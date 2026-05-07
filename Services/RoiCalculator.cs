using System.IO;
using System.Text.Json;

namespace InputStats;

public class RoiCalibrationEntry
{
    public DateTime Date { get; set; }
    public long WorkClicks { get; set; }
    public long WorkKeys { get; set; }
    public double WorkCm { get; set; }
    public double UserRoi { get; set; }
}

public class RoiWeights
{
    public double WClicks { get; set; }
    public double WKeys { get; set; }
    public double WCm { get; set; }
    public int CalibrationCount { get; set; }
    public bool IsCalibrated => CalibrationCount >= 3;
    public List<RoiCalibrationEntry> History { get; set; } = new();
}

public static class RoiStorage
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InputStats",
        "roi.json");

    public static RoiWeights Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<RoiWeights>(json) ?? new RoiWeights();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("加载 ROI 数据失败", ex);
        }
        return new RoiWeights();
    }

    public static void Save(RoiWeights weights)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(weights));
            Logger.Info($"ROI 数据已保存，校准次数: {weights.CalibrationCount}");
        }
        catch (Exception ex)
        {
            Logger.Error("保存 ROI 数据失败", ex);
        }
    }
}

public static class RoiCalculator
{
    private const double Epsilon = 1e-10;

    public static double FeatureTransform(long clicks, long keys, double cm)
    {
        // 使用对数变换压缩量级差异
        return Math.Log(clicks + 1);
    }

    public static bool TryComputeWeights(List<RoiCalibrationEntry> entries, out double wClicks, out double wKeys, out double wCm)
    {
        wClicks = wKeys = wCm = 0;

        if (entries.Count < 3)
            return false;

        // 取最近的所有记录（至少3条）
        var recent = entries.OrderByDescending(e => e.Date).Take(Math.Max(3, entries.Count)).OrderBy(e => e.Date).ToList();
        int n = recent.Count;

        // 构建设计矩阵 X (n×3) 和观测向量 y (n×1)
        var X = new double[n, 3];
        var y = new double[n];

        for (int i = 0; i < n; i++)
        {
            X[i, 0] = Math.Log(recent[i].WorkClicks + 1);
            X[i, 1] = Math.Log(recent[i].WorkKeys + 1);
            X[i, 2] = Math.Log(recent[i].WorkCm + 1);
            y[i] = recent[i].UserRoi;
        }

        // 计算 X^T * X (3×3)
        var XtX = new double[3, 3];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                for (int k = 0; k < n; k++)
                    XtX[i, j] += X[k, i] * X[k, j];

        // 计算 det(X^T * X)
        double det = Determinant3x3(XtX);
        if (Math.Abs(det) < Epsilon)
        {
            Logger.Warn("ROI 权重计算失败：校准数据线性相关，无法求逆矩阵");
            return false;
        }

        // 求 (X^T * X)^(-1)
        var adj = Adjugate3x3(XtX);
        var invXtX = new double[3, 3];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                invXtX[i, j] = adj[i, j] / det;

        // 计算 X^T * y (3×1)
        var Xty = new double[3];
        for (int i = 0; i < 3; i++)
            for (int k = 0; k < n; k++)
                Xty[i] += X[k, i] * y[k];

        // w = (X^T * X)^(-1) * X^T * y
        wClicks = invXtX[0, 0] * Xty[0] + invXtX[0, 1] * Xty[1] + invXtX[0, 2] * Xty[2];
        wKeys   = invXtX[1, 0] * Xty[0] + invXtX[1, 1] * Xty[1] + invXtX[1, 2] * Xty[2];
        wCm     = invXtX[2, 0] * Xty[0] + invXtX[2, 1] * Xty[1] + invXtX[2, 2] * Xty[2];

        Logger.Info($"ROI 权重计算完成（基于{n}条记录）：Clicks={wClicks:F6}, Keys={wKeys:F6}, Cm={wCm:F6}");
        return true;
    }

    public static double ComputeRoi(long workClicks, long workKeys, double workCm, double wClicks, double wKeys, double wCm)
    {
        double roi = wClicks * Math.Log(workClicks + 1)
                   + wKeys * Math.Log(workKeys + 1)
                   + wCm * Math.Log(workCm + 1);

        // 限制在 0.01 - 1.99 范围内
        if (roi < 0.01) roi = 0.01;
        if (roi > 1.99) roi = 1.99;

        return roi;
    }

    private static double Determinant3x3(double[,] m)
    {
        return m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
             - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
             + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
    }

    private static double[,] Adjugate3x3(double[,] m)
    {
        var adj = new double[3, 3];
        adj[0, 0] =  (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]);
        adj[0, 1] = -(m[0, 1] * m[2, 2] - m[0, 2] * m[2, 1]);
        adj[0, 2] =  (m[0, 1] * m[1, 2] - m[0, 2] * m[1, 1]);
        adj[1, 0] = -(m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0]);
        adj[1, 1] =  (m[0, 0] * m[2, 2] - m[0, 2] * m[2, 0]);
        adj[1, 2] = -(m[0, 0] * m[1, 2] - m[0, 2] * m[1, 0]);
        adj[2, 0] =  (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
        adj[2, 1] = -(m[0, 0] * m[2, 1] - m[0, 1] * m[2, 0]);
        adj[2, 2] =  (m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0]);
        return adj;
    }
}
