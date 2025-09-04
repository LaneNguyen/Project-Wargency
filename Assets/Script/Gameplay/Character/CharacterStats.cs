using UnityEngine;
using System;

namespace Wargency.Gameplay
{
    // CharacterStats – bổ sung "saturating delta":
    // - Delta sẽ được clamp theo phần còn lại của biên [0..Max] trước khi cộng,
    //   tránh các cú "vọt-qua-biên" gây cảm giác như nhảy 100 → 0/âm
    // Quy ước:
    //   Energy: delta âm = giảm; Stress: delta dương = tăng

    // file này giữ Energy và Stress của nhân vật
    // ApplyDelta là cộng trừ kiểu an toàn để không vượt biên 0..Max
    // khi đổi số sẽ bắn event để UI biết đường cập nhật
    public class CharacterStats : MonoBehaviour
    {
        [Header("Giới hạn ngưỡng")]
        [SerializeField] private int maxEnergy = 100;
        [SerializeField] private int maxStress = 100;

        [Header("Runtime (read-only trong Inspector)")]
        [SerializeField] private int energy = 100;
        [SerializeField] private int stress = 0;

        [Header("Behavior")]
        [Tooltip("Nếu bật, delta sẽ được clamp theo biên còn lại để không vượt 0..Max.")]
        public bool saturateDelta = true;

        // <summary>Event bắn ra mỗi khi chỉ số thay đổi: (energy, stress)</summary>
        public event Action<int, int> StatsChanged;

        public int MaxEnergy
        {
            get => maxEnergy;
            set => maxEnergy = Mathf.Max(1, value);
        }

        public int MaxStress
        {
            get => maxStress;
            set => maxStress = Mathf.Max(1, value);
        }

        public int Energy
        {
            get => energy;
            private set
            {
                int prevE = energy;
                energy = Mathf.Clamp(value, 0, MaxEnergy);
                if (energy != prevE) StatsChanged?.Invoke(energy, stress);
            }
        }

        public int Stress
        {
            get => stress;
            private set
            {
                int prevS = stress;
                stress = Mathf.Clamp(value, 0, MaxStress);
                if (stress != prevS) StatsChanged?.Invoke(energy, stress);
            }
        }

        // <summary>
        // Khởi tạo chỉ số từ Definition (nếu dùng)
        // </summary>
        public void InitFrom(CharacterDefinition def)
        {
            MaxEnergy = 100;
            MaxStress = 100;
            Energy = Mathf.Clamp(def.BaseEnergy, 0, MaxEnergy);
            Stress = Mathf.Clamp(def.BaseStress, 0, MaxStress);
        }

        // Cộng delta với "saturating add":
        //   - Nếu saturateDelta = true, sẽ clamp dEnergy vào [-Energy, MaxEnergy - Energy]
        //     và dStress vào [-Stress, MaxStress - Stress] trước khi cộng
        //   - Đảm bảo không bao giờ vượt biên hoặc tạo cảm giác nhảy 100 → 0/âm do delta quá lớn
        public void ApplyDelta(int dEnergy, int dStress)
        {
            int dE = dEnergy;
            int dS = dStress;

            if (saturateDelta)
            {
                // Không cho vượt 0..Max theo từng trục
                dE = Mathf.Clamp(dE, -Energy, MaxEnergy - Energy);
                dS = Mathf.Clamp(dS, -Stress, MaxStress - Stress);
            }

            // Cộng theo quy ước: âm = giảm, dương = tăng
            Energy = Energy + dE;
            Stress = Stress + dS;
        }

        // Điểm năng suất 0..1 (tham khảo): càng nhiều Energy và càng ít Stress thì càng cao
        public float Productivity01()
        {
            float e = (float)Energy / MaxEnergy;
            float s = (float)Stress / MaxStress;
            return Mathf.Clamp01(e * (1f - s));
        }

        //>Bắn trạng thái hiện tại (hữu ích sau thay đổi lớn)
        [ContextMenu("DEBUG: Notify HUD")]
        public void Notify()
        {
            StatsChanged?.Invoke(Energy, Stress);
        }

        // Debug helper
        [ContextMenu("DEBUG: -10 Energy")] private void __DbgE() => ApplyDelta(-10, 0);
        [ContextMenu("DEBUG: +10 Stress")] private void __DbgS() => ApplyDelta(0, +10);
    }
}