using UnityEngine;
using System;

namespace Wargency.Gameplay
{ 
public class CharacterStats : MonoBehaviour
{
        [Header("Giới hạn ngưỡng")]
        [SerializeField] private int maxEnergy = 100;
        [SerializeField] private int maxStress = 100;

        [Header("Runtime (ReadOnly tại inpector lúc chạy thôi nhe")]
        [SerializeField] private int energy;
        [SerializeField] private int stress;

        public event Action<int,int> StatsChanged; //energy với stress thôi nhé
        public int MaxEnergy { get => maxEnergy; set => maxEnergy = Mathf.Max(1, value); /*set nhận value nhưn lọc lại thành 1 mới xài*/}
        public int MaxStress { get => maxStress; set => maxStress = Mathf.Max(1, value); }

        public int Energy
        {
            get => energy;
            private set
            {
                energy = Mathf.Clamp(value, 0, MaxEnergy); // ép từ 0 đến MaxEnergy
                StatsChanged?.Invoke(energy, stress);      // bắn tin event mỗi khi đổi
            }
        }

        public int Stress
        {
            get => stress;
            private set //bên ngoài đọc dc nhưng ko set dc, để vậy cho chắc
            {
                energy = Math.Clamp(value, 0, MaxStress);  
                StatsChanged?.Invoke(energy, stress);
            }
        }

        public void InitFrom(CharacterDefinition def) //Initialize from là khởi tạo từ, ghi tắt cho gọn
        {
            MaxEnergy = 100;
            MaxStress = 100;
            Energy = Mathf.Clamp(def.BaseEnergy, 0, MaxEnergy);
            Stress = Mathf.Clamp(def.BaseStress, 0, MaxStress);
        }
        public void ApplyDelta(int dEnergy, int dStress) //thay đổi có kiềm soát tùy theo hoạt activities
        {
            Energy = Energy + dEnergy;
            Stress = Stress + dStress;
        }

        public float Productivity01() //script tính năng suất để consider sử dụng sau
        {
            float e = (float)Energy / MaxEnergy;
            float s = (float)Stress / MaxStress;
            return Mathf.Clamp01(e * (1f - s));
        }

    }
}
