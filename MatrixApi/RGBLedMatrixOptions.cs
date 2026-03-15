namespace MatrixApi;

public struct RGBLedMatrixOptions
{
    public string? HardwareMapping = null;
    public int Rows = 32;
    public int Cols = 32;
    public int ChainLength = 1;
    public int Parallel = 1;
    public int PwmBits = 11;
    public int PwmLsbNanoseconds = 130;
    public int PwmDitherBits = 0;
    public int Brightness = 100;
    public ScanModes ScanMode = ScanModes.Progressive;
    public int RowAddressType = 0;
    public Multiplexing Multiplexing = Multiplexing.Direct;
    public string? LedRgbSequence = null;
    public string? PixelMapperConfig = null;
    public string? PanelType = null;
    public bool DisableHardwarePulsing = false;
    public bool ShowRefreshRate = false;
    public bool InverseColors = false;
    public int LimitRefreshRateHz = 0;
    public int GpioSlowdown = 1;

    public RGBLedMatrixOptions()
    {
    }
}
