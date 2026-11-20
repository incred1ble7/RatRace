using System.Linq;
using UnityEngine;

namespace MouseGame
{
    public class KitchenMap : MonoBehaviour
    {
        public static KitchenMap Instance { get; private set; }

        [SerializeField] SpawnPoint[] spawns;
        [SerializeField] MouseHole[] holes;
        [SerializeField] FoodSpawnPoint[] foodPoints;
        [SerializeField] CatZone[] catZones;
        [SerializeField] CatSpot catSpot;

        public SpawnPoint[] Spawns => spawns;
        public MouseHole[] Holes => holes;
        public FoodSpawnPoint[] FoodPoints => foodPoints;
        public CatZone[] CatZones => catZones;
        public CatSpot CatSpot => catSpot;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public SpawnPoint GetSpawn(int slot) => spawns.Length == 0 ? null : spawns[(Mathf.Max(1, slot) - 1) % spawns.Length];
        public MouseHole GetHole(int slot) => holes.FirstOrDefault(h => h.index == slot);

#if UNITY_EDITOR
        public void Collect()
        {
            spawns = GetComponentsInChildren<SpawnPoint>(true).OrderBy(s => s.index).ToArray();
            holes = GetComponentsInChildren<MouseHole>(true).OrderBy(h => h.index).ToArray();
            foodPoints = GetComponentsInChildren<FoodSpawnPoint>(true).ToArray();
            catZones = GetComponentsInChildren<CatZone>(true).OrderBy(z => z.index).ToArray();
            catSpot = GetComponentInChildren<CatSpot>(true);
        }
#endif
    }
}
