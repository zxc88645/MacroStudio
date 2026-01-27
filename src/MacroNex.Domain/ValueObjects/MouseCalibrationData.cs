namespace MacroNex.Domain.ValueObjects;

/// <summary>
/// 滑鼠校準數據，用於 HID ΔXY 到實際像素的轉換。
/// 支援非線性模型（考慮 Windows 滑鼠加速）。
/// 使用多項式回歸進行智能擬合。
/// </summary>
public class MouseCalibrationData
{
    /// <summary>
    /// 校準時間
    /// </summary>
    public DateTime CalibratedAt { get; set; }

    /// <summary>
    /// X 軸校準樣本點 (HID Delta -> Actual Pixel)
    /// </summary>
    public List<CalibrationPoint> PointsX { get; set; } = new();

    /// <summary>
    /// Y 軸校準樣本點 (HID Delta -> Actual Pixel)
    /// </summary>
    public List<CalibrationPoint> PointsY { get; set; } = new();

    /// <summary>
    /// X 軸多項式係數 (a0 + a1*x + a2*x^2)，用於 Pixel -> HID 轉換
    /// </summary>
    public double[] PolynomialCoefficientsX { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Y 軸多項式係數
    /// </summary>
    public double[] PolynomialCoefficientsY { get; set; } = Array.Empty<double>();

    /// <summary>
    /// 是否檢測到滑鼠加速（非線性行為）
    /// </summary>
    public bool HasMouseAcceleration { get; set; }

    /// <summary>
    /// 根據目標像素移動量，計算需要發送的 HID delta（反向查找）。
    /// 優先使用多項式模型，fallback 到查表插值。
    /// </summary>
    /// <param name="targetPixelDelta">目標像素移動量</param>
    /// <param name="useYAxis">是否使用 Y 軸數據</param>
    /// <returns>需要發送的 HID delta 值</returns>
    public int CalculateHidDelta(double targetPixelDelta, bool useYAxis = false)
    {
        var coefficients = useYAxis ? PolynomialCoefficientsY : PolynomialCoefficientsX;
        var points = useYAxis ? PointsY : PointsX;
        
        if (points.Count == 0)
            return (int)Math.Round(targetPixelDelta); // 無校準數據，假設 1:1

        // 處理負值：取絕對值計算後再恢復符號
        bool isNegative = targetPixelDelta < 0;
        double absTarget = Math.Abs(targetPixelDelta);

        // 優先使用多項式模型
        if (coefficients.Length >= 2)
        {
            double hidDelta = EvaluatePolynomial(absTarget, coefficients);
            // 確保結果合理（不為負數且不過大）
            if (hidDelta >= 0 && hidDelta < 10000)
            {
                int result = (int)Math.Round(hidDelta);
                return isNegative ? -result : result;
            }
        }

        // Fallback: 使用查表插值
        return CalculateHidDeltaByInterpolation(absTarget, points, isNegative);
    }

    private int CalculateHidDeltaByInterpolation(double absTarget, List<CalibrationPoint> points, bool isNegative)
    {
        // 排序確保點按 ActualPixelDelta 升序排列
        var sortedPoints = points
            .Where(p => p.ActualPixelDelta >= 0)
            .OrderBy(p => p.ActualPixelDelta)
            .ToList();

        if (sortedPoints.Count == 0)
            return (int)Math.Round(isNegative ? -absTarget : absTarget);

        // 邊界處理
        if (absTarget <= sortedPoints[0].ActualPixelDelta)
        {
            double ratio = sortedPoints[0].HidDelta / Math.Max(sortedPoints[0].ActualPixelDelta, 0.001);
            int result = (int)Math.Round(absTarget * ratio);
            return isNegative ? -result : result;
        }

        if (absTarget >= sortedPoints[^1].ActualPixelDelta)
        {
            double ratio = sortedPoints[^1].HidDelta / Math.Max(sortedPoints[^1].ActualPixelDelta, 0.001);
            int result = (int)Math.Round(absTarget * ratio);
            return isNegative ? -result : result;
        }

        // 使用 Catmull-Rom 樣條插值（比線性插值更平滑）
        for (int i = 0; i < sortedPoints.Count - 1; i++)
        {
            var p1 = sortedPoints[i];
            var p2 = sortedPoints[i + 1];

            if (absTarget >= p1.ActualPixelDelta && absTarget <= p2.ActualPixelDelta)
            {
                double t = (absTarget - p1.ActualPixelDelta) / 
                           Math.Max(p2.ActualPixelDelta - p1.ActualPixelDelta, 0.001);
                
                // 使用 Hermite 插值獲得更平滑的曲線
                double hidDelta = HermiteInterpolate(
                    i > 0 ? sortedPoints[i - 1].HidDelta : p1.HidDelta,
                    p1.HidDelta,
                    p2.HidDelta,
                    i < sortedPoints.Count - 2 ? sortedPoints[i + 2].HidDelta : p2.HidDelta,
                    t);
                
                int result = (int)Math.Round(hidDelta);
                return isNegative ? -result : result;
            }
        }

        return (int)Math.Round(isNegative ? -absTarget : absTarget);
    }

    /// <summary>
    /// Hermite 樣條插值，提供比線性插值更平滑的曲線
    /// </summary>
    private static double HermiteInterpolate(double y0, double y1, double y2, double y3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;
        
        double a0 = -0.5 * y0 + 1.5 * y1 - 1.5 * y2 + 0.5 * y3;
        double a1 = y0 - 2.5 * y1 + 2 * y2 - 0.5 * y3;
        double a2 = -0.5 * y0 + 0.5 * y2;
        double a3 = y1;
        
        return a0 * t3 + a1 * t2 + a2 * t + a3;
    }

    /// <summary>
    /// 計算多項式的值
    /// </summary>
    private static double EvaluatePolynomial(double x, double[] coefficients)
    {
        double result = 0;
        double xPower = 1;
        for (int i = 0; i < coefficients.Length; i++)
        {
            result += coefficients[i] * xPower;
            xPower *= x;
        }
        return result;
    }

    /// <summary>
    /// 使用最小二乘法擬合多項式（Pixel -> HID）
    /// </summary>
    public void FitPolynomial(int degree = 2)
    {
        PolynomialCoefficientsX = FitPolynomialForAxis(PointsX, degree);
        PolynomialCoefficientsY = FitPolynomialForAxis(PointsY, degree);
        
        // 檢測是否存在顯著的非線性（滑鼠加速）
        HasMouseAcceleration = DetectMouseAcceleration();
    }

    private double[] FitPolynomialForAxis(List<CalibrationPoint> points, int degree)
    {
        if (points.Count < degree + 1)
            return Array.Empty<double>();

        var validPoints = points.Where(p => p.HidDelta > 0 && p.ActualPixelDelta > 0).ToList();
        if (validPoints.Count < degree + 1)
            return Array.Empty<double>();

        int n = validPoints.Count;
        int m = degree + 1;

        // 建立 Vandermonde 矩陣進行最小二乘擬合
        // 我們要擬合 HID = f(Pixel)，所以 X 是 Pixel，Y 是 HID
        double[,] A = new double[n, m];
        double[] b = new double[n];

        for (int i = 0; i < n; i++)
        {
            double x = validPoints[i].ActualPixelDelta;
            double xPower = 1;
            for (int j = 0; j < m; j++)
            {
                A[i, j] = xPower;
                xPower *= x;
            }
            b[i] = validPoints[i].HidDelta;
        }

        // 使用正規方程求解：(A^T * A) * x = A^T * b
        return SolveNormalEquations(A, b, n, m);
    }

    private static double[] SolveNormalEquations(double[,] A, double[] b, int n, int m)
    {
        // 計算 A^T * A
        double[,] AtA = new double[m, m];
        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < m; j++)
            {
                double sum = 0;
                for (int k = 0; k < n; k++)
                    sum += A[k, i] * A[k, j];
                AtA[i, j] = sum;
            }
        }

        // 計算 A^T * b
        double[] Atb = new double[m];
        for (int i = 0; i < m; i++)
        {
            double sum = 0;
            for (int k = 0; k < n; k++)
                sum += A[k, i] * b[k];
            Atb[i] = sum;
        }

        // 使用高斯消元法求解
        return GaussianElimination(AtA, Atb, m);
    }

    private static double[] GaussianElimination(double[,] A, double[] b, int n)
    {
        double[] x = new double[n];
        double[,] augmented = new double[n, n + 1];

        // 建立增廣矩陣
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                augmented[i, j] = A[i, j];
            augmented[i, n] = b[i];
        }

        // 前向消元
        for (int i = 0; i < n; i++)
        {
            // 找主元
            int maxRow = i;
            for (int k = i + 1; k < n; k++)
            {
                if (Math.Abs(augmented[k, i]) > Math.Abs(augmented[maxRow, i]))
                    maxRow = k;
            }

            // 交換行
            for (int k = i; k <= n; k++)
            {
                (augmented[i, k], augmented[maxRow, k]) = (augmented[maxRow, k], augmented[i, k]);
            }

            // 消元
            for (int k = i + 1; k < n; k++)
            {
                if (Math.Abs(augmented[i, i]) < 1e-10) continue;
                double factor = augmented[k, i] / augmented[i, i];
                for (int j = i; j <= n; j++)
                    augmented[k, j] -= factor * augmented[i, j];
            }
        }

        // 回代
        for (int i = n - 1; i >= 0; i--)
        {
            if (Math.Abs(augmented[i, i]) < 1e-10)
            {
                x[i] = 0;
                continue;
            }
            x[i] = augmented[i, n];
            for (int j = i + 1; j < n; j++)
                x[i] -= augmented[i, j] * x[j];
            x[i] /= augmented[i, i];
        }

        return x;
    }

    /// <summary>
    /// 檢測是否存在滑鼠加速（非線性行為）
    /// </summary>
    private bool DetectMouseAcceleration()
    {
        // 計算各點的比例，如果變化超過閾值則認為存在加速
        var ratiosX = PointsX.Where(p => p.HidDelta > 0).Select(p => p.Ratio).ToList();
        var ratiosY = PointsY.Where(p => p.HidDelta > 0).Select(p => p.Ratio).ToList();

        bool accelerationX = ratiosX.Count >= 3 && CalculateVariationCoefficient(ratiosX) > 0.05;
        bool accelerationY = ratiosY.Count >= 3 && CalculateVariationCoefficient(ratiosY) > 0.05;

        return accelerationX || accelerationY;
    }

    private static double CalculateVariationCoefficient(List<double> values)
    {
        if (values.Count < 2) return 0;
        double mean = values.Average();
        if (Math.Abs(mean) < 0.001) return 0;
        double variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        return Math.Sqrt(variance) / Math.Abs(mean);
    }

    /// <summary>
    /// 根據 HID delta 計算預期的實際像素移動量（正向查找）。
    /// 用於預覽校準效果。
    /// </summary>
    /// <param name="hidDelta">HID delta 值</param>
    /// <param name="useYAxis">是否使用 Y 軸數據</param>
    /// <returns>預期的像素移動量</returns>
    public double CalculatePixelDelta(int hidDelta, bool useYAxis = false)
    {
        var points = useYAxis ? PointsY : PointsX;
        
        if (points.Count == 0)
            return hidDelta; // 無校準數據，假設 1:1

        // 處理負值
        bool isNegative = hidDelta < 0;
        int absHid = Math.Abs(hidDelta);

        // 排序確保點按 HidDelta 升序排列
        var sortedPoints = points
            .Where(p => p.HidDelta >= 0)
            .OrderBy(p => p.HidDelta)
            .ToList();

        if (sortedPoints.Count == 0)
            return hidDelta;

        // 邊界處理
        if (absHid <= sortedPoints[0].HidDelta)
        {
            double ratio = sortedPoints[0].ActualPixelDelta / Math.Max(sortedPoints[0].HidDelta, 0.001);
            double result = absHid * ratio;
            return isNegative ? -result : result;
        }

        if (absHid >= sortedPoints[^1].HidDelta)
        {
            double ratio = sortedPoints[^1].ActualPixelDelta / Math.Max(sortedPoints[^1].HidDelta, 0.001);
            double result = absHid * ratio;
            return isNegative ? -result : result;
        }

        // 線性插值
        for (int i = 0; i < sortedPoints.Count - 1; i++)
        {
            var p1 = sortedPoints[i];
            var p2 = sortedPoints[i + 1];

            if (absHid >= p1.HidDelta && absHid <= p2.HidDelta)
            {
                double t = (absHid - p1.HidDelta) / Math.Max(p2.HidDelta - p1.HidDelta, 0.001);
                double pixelDelta = p1.ActualPixelDelta + t * (p2.ActualPixelDelta - p1.ActualPixelDelta);
                return isNegative ? -pixelDelta : pixelDelta;
            }
        }

        return hidDelta;
    }

    /// <summary>
    /// 檢查校準數據是否有效
    /// </summary>
    public bool IsValid => PointsX.Count >= 2 || PointsY.Count >= 2;

    /// <summary>
    /// 取得校準摘要資訊
    /// </summary>
    public string GetSummary()
    {
        if (!IsValid)
            return "無有效校準數據";

        var avgRatioX = PointsX.Count > 0
            ? PointsX.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
            : 0;
        var avgRatioY = PointsY.Count > 0
            ? PointsY.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
            : 0;

        return $"校準於 {CalibratedAt:yyyy-MM-dd HH:mm}, X 平均比例: {avgRatioX:F3}, Y 平均比例: {avgRatioY:F3}";
    }
}

/// <summary>
/// 單個校準點，記錄 HID Delta 和對應的實際像素移動量
/// </summary>
public class CalibrationPoint
{
    /// <summary>
    /// 發送的 HID Delta 值
    /// </summary>
    public int HidDelta { get; set; }

    /// <summary>
    /// 實際測量的像素移動量
    /// </summary>
    public double ActualPixelDelta { get; set; }

    /// <summary>
    /// 計算比例 (ActualPixel / HidDelta)
    /// </summary>
    public double Ratio => HidDelta != 0 ? ActualPixelDelta / HidDelta : 0;

    public override string ToString() => $"HID: {HidDelta} -> Pixel: {ActualPixelDelta:F1} (Ratio: {Ratio:F3})";
}
