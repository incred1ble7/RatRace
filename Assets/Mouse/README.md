# Mouse — low-poly мышь для мобильной игры

## Что внутри

| Папка | Содержимое |
|---|---|
| `Models/Mouse.fbx` | Мышь: 652 треугольника, 378 вершин, 15 костей, клипы `Idle`, `Run`, `Carry` |
| `Models/Cheese.fbx` | Кусок сыра (пример еды для переноски), 20 треугольников |
| `Textures/T_Mouse_Palette.png` | Палитра 64×8 px (1,5 КБ) — одна текстура и один материал на всё |
| `Scripts/MouseVisual.cs` | Рантайм-компонент: скорость → анимация, взять/положить еду |
| `Scripts/Editor/*` | Настройки импорта под мобилки + сборка префаба |

После импорта автоматически создаются:
`Prefabs/Mouse.prefab`, `Prefabs/Cheese.prefab`, `Materials/M_Mouse.mat`,
`Animation/AC_Mouse.controller`, `Animation/AM_Mouse_Carry.mask`.
Пересобрать вручную: **Tools → Mouse → Rebuild Prefab**.

## Установка

1. Скопировать папку `Mouse` в `Assets/` проекта (Unity 2021.3+, Built-in или URP).
2. Дождаться импорта — префаб соберётся сам.
3. Положить `Prefabs/Mouse.prefab` в сцену.

Модель смотрит вдоль **+Z**, пивот на земле, масштаб 1 = 1 м
(длина тела ~0,5 м, рост ~0,25 м — при необходимости масштабируйте корень префаба).

## Использование

```csharp
using MouseGame;

public class MouseController : MonoBehaviour
{
    [SerializeField] MouseVisual visual;

    void Update()
    {
        float speed = /* скорость из вашего движения, м/с */ 0f;
        visual.SetSpeed(speed);                 // 0 → Idle, > 0.05 → Run
    }

    void OnFoodReached(Transform food) => visual.PickUp(food);   // лапы поднимаются, еда в лапах
    void OnHomeReached()               => visual.Drop();          // еда отпущена
}
```

* `referenceRunSpeed` в `MouseVisual` — скорость (м/с), при которой бег проигрывается 1×.
  Подстройте, чтобы лапы не «скользили».
* Еда крепится к кости `Food_Socket` пивотом. Для своей еды сделайте пивот в центре модели;
  поворот подправляется полем `foodRotationOffset`.

## Как устроен Animator

* **Base Layer:** `Idle` ⇄ `Run` по параметру `Speed`, скорость бега — параметр `RunSpeed`.
* **Carry Layer:** Override с маской только на `Head`, `Leg_FL`, `Leg_FR`.
  Вес слоя плавно 0↔1 ставит `MouseVisual`, поэтому переноска работает и в покое, и на бегу
  (задние лапы, тело и хвост продолжают анимироваться).

## Оптимизация под мобилки (применяется автоматически)

* 652 tris, 1 материал, 1 текстура 64×8 без мипмапов → 1 draw call на мышь, сыр в том же материале.
* Skinning: 2 кости на вершину (`SkinQuality.Bone2`).
* `Optimize Game Objects`: кости не создаются как GameObject (наружу выставлен только `Food_Socket`).
* Анимации: сжатие `Optimal`; меш: `Mesh Compression = Medium`, `Read/Write` выкл., без тангентов.
* Animator: `Cull Update Transforms` — вне экрана скиннинг не считается.
* Шейдер: URP `Simple Lit` или `Mobile/Diffuse` для Built-in (выбирается автоматически).

Размер `Mouse.fbx` на диске (~300 КБ) в билд не попадает: Unity хранит меш и сжатые клипы
в своём формате, в билде мышь занимает существенно меньше.

## Исходники

`Source~/build_mouse.py` — скрипт Blender, который строит модель, риг, анимации и экспортирует FBX
(`blender -b -P build_mouse.py` или `pip install bpy` → `python build_mouse.py`).
`Source~/Mouse.blend` — сцена Blender. Папки с `~` Unity не импортирует.
