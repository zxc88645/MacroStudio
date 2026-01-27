namespace MacroNex.Domain.ValueObjects;

/// <summary>
/// 滑鼠校準數據，用於 HID ΔXY 到實際像素的轉換。
/// 基於 Microsoft Windows Pointer Ballistics 官方文檔實現。
/// 支援非線性模型（考慮 Windows 滑鼠加速/增強指標精確度）。
/// </summary>
/// <remarks>
/// Windows 滑鼠加速算法要點：
/// 1. 使用向量大小（magnitude）計算加速倍數，而非分別處理 X/Y
/// 2. 使用 5 點查找表進行線性插值
/// 3. 保留子像素餘數以獲得平滑移動
/// 4. 物理單位轉換：速度 = mickey × UpdateRate / DPI
/// </remarks>
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
    /// 加速曲線類型
    /// </summary>
    public AccelerationCurveType CurveType { get; set; } = AccelerationCurveType.Linear;

    /// <summary>
    /// Windows 加速曲線的 5 個控制點（速度閾值）
    /// </summary>
    public double[] AccelerationThresholds { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Windows 加速曲線的 5 個控制點（對應增益）
    /// </summary>
    public double[] AccelerationGains { get; set; } = Array.Empty<double>();

    /// <summary>
    /// 根據目標像素移動量，計算需要發送的 HID delta（反向查找）。
    /// 基於 Windows Pointer Ballistics 算法實現。
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

        // 方法選擇：根據加速曲線類型
        double hidDelta;
        
        if (CurveType == AccelerationCurveType.WindowsEnhanced && 
            AccelerationThresholds.Length >= 2 && AccelerationGains.Length >= 2)
        {
            // 使用 Windows 風格的加速曲線（反向計算）
            hidDelta = CalculateHidDeltaByWindowsCurve(absTarget);
        }
        else if (coefficients.Length >= 2)
        {
            // 使用多項式模型
            hidDelta = EvaluatePolynomial(absTarget, coefficients);
            if (hidDelta < 0 || hidDelta >= 10000)
            {
                // 多項式結果不合理，fallback
                hidDelta = CalculateHidDeltaByInterpolationInternal(absTarget, points);
            }
        }
        else
        {
            // Fallback: 使用查表插值
            hidDelta = CalculateHidDeltaByInterpolationInternal(absTarget, points);
        }

        int result = (int)Math.Round(hidDelta);
        return isNegative ? -result : result;
    }

    /// <summary>
    /// 使用 Windows 風格加速曲線反向計算 HID delta
    /// Windows 公式：PointerVelocity = MouseVelocity × Gain
    /// 反向：MouseVelocity = PointerVelocity / Gain
    /// </summary>
    private double CalculateHidDeltaByWindowsCurve(double targetPixelDelta)
    {
        // 找到目標像素值對應的加速增益（反向查找）
        // 由於加速是速度相關的，我們需要模擬 Windows 的行為
        
        // 首先估計對應的增益區間
        // 假設：targetPixel = hidDelta × gain
        // 所以：hidDelta = targetPixel / gain
        
        // 使用二分搜尋找到正確的 HID delta
        double low = 0;
        double high = targetPixelDelta * 2; // 初始上界
        
        for (int iteration = 0; iteration < 50; iteration++)
        {
            double mid = (low + high) / 2;
            double predictedPixel = CalculatePixelDeltaByWindowsCurve(mid);
            
            if (Math.Abs(predictedPixel - targetPixelDelta) < 0.1)
                return mid;
            
            if (predictedPixel < targetPixelDelta)
                low = mid;
            else
                high = mid;
        }
        
        return (low + high) / 2;
    }

    /// <summary>
    /// 使用 Windows 風格加速曲線計算像素移動量
    /// 根據 Microsoft Pointer Ballistics 文檔實現
    /// </summary>
    private double CalculatePixelDeltaByWindowsCurve(double hidDelta)
    {
        if (AccelerationThresholds.Length < 2 || AccelerationGains.Length < 2)
            return hidDelta;

        double absHid = Math.Abs(hidDelta);
        
        // 在閾值點之間進行線性插值查找增益
        double gain = 1.0;
        
        for (int i = 0; i < AccelerationThresholds.Length - 1; i++)
        {
            double t1 = AccelerationThresholds[i];
            double t2 = AccelerationThresholds[i + 1];
            double g1 = AccelerationGains[i];
            double g2 = AccelerationGains[i + 1];
            
            if (absHid >= t1 && absHid <= t2)
            {
                // 線性插值
                double t = (t2 - t1) > 0.001 ? (absHid - t1) / (t2 - t1) : 0;
                gain = g1 + t * (g2 - g1);
                break;
            }
            else if (absHid < t1 && i == 0)
            {
                // 低於最低閾值
                gain = t1 > 0.001 ? (g1 / t1) * absHid : g1;
                break;
            }
            else if (absHid > t2 && i == AccelerationThresholds.Length - 2)
            {
                // 高於最高閾值，線性外推
                double slope = (g2 - g1) / Math.Max(t2 - t1, 0.001);
                gain = g2 + slope * (absHid - t2);
                break;
            }
        }

        return absHid * gain;
    }

    private double CalculateHidDeltaByInterpolationInternal(double absTarget, List<CalibrationPoint> points)
    {
        // 排序確保點按 ActualPixelDelta 升序排列
        var sortedPoints = points
            .Where(p => p.ActualPixelDelta >= 0)
            .OrderBy(p => p.ActualPixelDelta)
            .ToList();

        if (sortedPoints.Count == 0)
            return absTarget;

        // 邊界處理
        if (absTarget <= sortedPoints[0].ActualPixelDelta)
        {
            double ratio = sortedPoints[0].HidDelta / Math.Max(sortedPoints[0].ActualPixelDelta, 0.001);
            return absTarget * ratio;
        }

        if (absTarget >= sortedPoints[^1].ActualPixelDelta)
        {
            double ratio = sortedPoints[^1].HidDelta / Math.Max(sortedPoints[^1].ActualPixelDelta, 0.001);
            return absTarget * ratio;
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
                return HermiteInterpolate(
                    i > 0 ? sortedPoints[i - 1].HidDelta : p1.HidDelta,
                    p1.HidDelta,
                    p2.HidDelta,
                    i < sortedPoints.Count - 2 ? sortedPoints[i + 2].HidDelta : p2.HidDelta,
                    t);
            }
        }

        return absTarget;
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
    /// <summary>
    /// 分析校準數據並擬合最佳模型
    /// </summary>
    public void FitPolynomial(int degree = 2)
    {
        // 首先檢測是否存在滑鼠加速
        HasMouseAcceleration = DetectMouseAcceleration();

        if (HasMouseAcceleration)
        {
            // 檢測到加速，嘗試擬合 Windows 風格加速曲線
            FitWindowsAccelerationCurve();
            CurveType = AccelerationCurveType.WindowsEnhanced;
        }
        else
        {
            // 線性行為，使用簡單多項式
            CurveType = AccelerationCurveType.Linear;
        }

        // 同時擬合多項式作為備用
        PolynomialCoefficientsX = FitPolynomialForAxis(PointsX, degree);
        PolynomialCoefficientsY = FitPolynomialForAxis(PointsY, degree);
    }

    /// <summary>
    /// 擬合 Windows 風格的加速曲線
    /// 根據校準點推算出速度閾值和對應增益
    /// </summary>
    private void FitWindowsAccelerationCurve()
    {
        // 合併 X 和 Y 的校準數據（因為 Windows 使用向量大小）
        var allPoints = PointsX.Concat(PointsY)
            .Where(p => p.HidDelta > 0 && p.ActualPixelDelta > 0)
            .OrderBy(p => p.HidDelta)
            .ToList();

        if (allPoints.Count < 3)
            return;

        // 提取閾值和增益
        // 閾值 = HID delta（代表滑鼠移動速度）
        // 增益 = ActualPixelDelta / HidDelta（速度倍增因子）
        var thresholds = new List<double> { 0 };
        var gains = new List<double> { allPoints[0].Ratio };

        // 選擇關鍵點作為曲線控制點
        // Windows 使用 5 個點，我們根據數據分布選擇
        int step = Math.Max(1, (allPoints.Count - 1) / 4);
        
        for (int i = step; i < allPoints.Count; i += step)
        {
            var point = allPoints[Math.Min(i, allPoints.Count - 1)];
            thresholds.Add(point.HidDelta);
            gains.Add(point.Ratio);
        }

        // 確保包含最後一個點
        if (thresholds.Count < 5 && allPoints.Count > 0)
        {
            var lastPoint = allPoints[^1];
            if (thresholds[^1] != lastPoint.HidDelta)
            {
                thresholds.Add(lastPoint.HidDelta);
                gains.Add(lastPoint.Ratio);
            }
        }

        AccelerationThresholds = thresholds.ToArray();
        AccelerationGains = gains.ToArray();
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

/// <summary>
/// 加速曲線類型
/// </summary>
public enum AccelerationCurveType
{
    /// <summary>
    /// 線性模式（無加速或加速已禁用）
    /// </summary>
    Linear,

    /// <summary>
    /// Windows 增強指標精確度模式
    /// 使用 5 點查找表和線性插值
    /// </summary>
    WindowsEnhanced,

    /// <summary>
    /// 多項式模式（用於自定義曲線）
    /// </summary>
    Polynomial
}
