namespace Wargency.Gameplay
{
    // chỗ này chỉ nói độ khó thôi, mấy hệ khác hỏi để tự tăng giảm
    public interface IDifficultyProvider
    {
        float TaskSpeedMultiplier { get; } // làm task nhanh hay chậm
        float EnergyDrainPerSec { get; }   // tụt năng lượng mỗi giây
        float StressGainPerSec { get; }    // lên stress mỗi giây
    }
}
