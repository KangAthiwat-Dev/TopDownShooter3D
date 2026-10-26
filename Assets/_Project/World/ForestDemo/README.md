# Forest Demo — โซนป่าตัวอย่าง

ฉาก `Assets/_Project/Scenes/WorldPrototype.unity` มีโซนป่าตัวอย่างบนพื้นที่สูงเดิมรอบ Player ที่พิกัดโลกประมาณ **(-60, 5, -60)** ขนาดพื้นที่ตกแต่งประมาณ 60 × 60 เมตร เป็นตัวอย่างสำหรับต่อยอด ไม่ใช่แผนที่ทั้งโลก

## เปิดดู

1. เปิด `WorldPrototype` แล้วใช้เมนู **Tools > World > Focus Forest Demo**
2. วัตถุที่ตกแต่งอยู่ใต้ **World > Zone_ForestDemo** ส่วนพื้นยังเป็น **World > Terrain**
3. กด Play: WASD เคลื่อนที่, Shift วิ่ง, Space Dash, M แปลงร่าง, J โจมตี, I/Tab กระเป๋า

มีต้นไม้ใหญ่ 18 ต้น หิน พุ่มไม้ หญ้า Instancing ประมาณ 12,800 กอ ทางดินโค้ง และลานกลางสำหรับต่อสู้ จุดเกิดศัตรูเดิมทั้ง 6 จุดย้ายมาอยู่บนพื้นที่นี้ โดยไม่เปลี่ยนจำนวน Wave, damage, ความเร็ว หรือระบบ Player

## ทำไมพื้นเดิมมืดและยังไม่มีต้นไม้

- แพ็ก `Fantasy Worlds - DEMO Content` แบ่งเนื้อหาไว้ใน `.unitypackage` อีกชั้น เดิมมีเพียง Texture; นำเข้าไฟล์ **_URP Content - Fantasy Worlds - Old Forest DEMO.unitypackage** แล้ว ไม่ใช้เวอร์ชัน BiRP และไม่ได้เปิดฉาก Demo ทับฉากเกม
- พื้นเดิมมี Terrain Layer เดียวที่ใช้ **Soil** จึงเป็นสีน้ำตาล ไม่ใช่หญ้าแบบภาพอ้างอิง
- ตอนนี้ผสม Grass / Soil / Moss / Rock เฉพาะรอบโซนและไล่รอยต่อออกไป พื้นด้านนอกยังเป็นงานเดิม
- แยกวัสดุพื้น/หญ้าของโครงการ ปรับแสงแดดและแสงแวดล้อมในฉาก World ให้สว่างขึ้น พร้อม Local Volume สำหรับโทนของโซน ไม่แก้ profile ส่วนกลางที่ฉากอื่นใช้อยู่

## ปรับแต่งต่อใน Inspector

| อยากปรับ | ตำแหน่ง |
| --- | --- |
| เพิ่ม/ย้ายต้นไม้ หิน พุ่มไม้ | `World/Zone_ForestDemo/01_Trees`, `02_Rocks`, `03_Undergrowth`, `06_ClearingLandmarks` |
| เปลี่ยนสีหรือระยะจางของหญ้า | `Materials/M_ForestGrass.mat`: Color, Root Color, Fade Distance |
| เพิ่ม/ลบหญ้าเป็นบริเวณ | เลือก Terrain > Paint Details > ForestGrass; ใช้ Brush ทา/ลบ |
| ลดจำนวนหญ้าเพื่อประสิทธิภาพ | Terrain Settings > Detail Density หรือ Detail Distance (ตอนนี้ 65m) |
| ทาสีพื้นหญ้า/ทางดิน/หิน | Terrain > Paint Texture > เลือก Layer ที่ขึ้นต้น `Forest_` |
| ขนาดลายพื้น | `Layers/*.terrainlayer` > Tile Size (ส่วนใหญ่ 4m) |
| ความสว่าง/สีโดยไม่เปลี่ยน Texture | `05_ForestLook` > Volume > `ForestLook.asset`: Post Exposure / Contrast / Saturation |
| แสงแดดและเงา | Directional Light ในฉาก; ปัจจุบัน Intensity 3.5 และเงาอ่อน |
| ลมพัด | `04_GentleForestWind`: Wind Strength / Grass Wind Strength |

ต้นไม้และหินยังเชื่อมกับ prefab ของแพ็ก ไม่แก้ source prefab ต้นไม้แต่ละต้นใช้ Collider เฉพาะลำต้นแทนกลุ่ม Collider กิ่ง 28 ชิ้น เพื่อไม่ให้กิ่งที่มองไม่ชัดขวางการวิ่ง/ยิง ส่วนหญ้าและพุ่มเล็กไม่ขวางการเคลื่อนที่

หลังเปลี่ยนความสูงพื้นหรือย้ายสิ่งกีดขวาง ใช้ **Tools > World > Rebake Forest Navigation** แล้วบันทึกฉาก จากนั้นใช้ **Validate Forest Demo** ตรวจเส้นทางจากจุดเกิดทั้ง 6 จุด การย้ายลาน/ขยายพื้นที่ต้องตรวจ Player และ SpawnPoint ให้ตรงพื้นด้วย

## ไฟล์และการย้อนกลับ

- `ForestTerrain.asset`: สำเนา TerrainData ที่ใช้งานจริง ทั้ง Terrain และ TerrainCollider อ้างอิงไฟล์นี้
- `Assets/New Terrain 1.asset`: พื้นต้นฉบับที่ผู้ใช้ปั้นไว้ **ไม่ถูกแก้ไข** และตรวจ heightmap ของสำเนาแล้วตรงกันทุกจุด
- `ForestNavigation*.asset`: NavMesh ใหม่ของฉาก เก็บ baked asset เดิมใน `Scenes/WorldPrototype/NavMesh-Navigation.asset` ไว้ ไม่เขียนทับ
- `Layers/`, `Materials/`, `Prefabs/ForestGrass.prefab`, `ForestLook.asset`: ส่วนที่ปรับแต่งเฉพาะโซน
- `Logs/SceneBackups/WorldPrototype_BeforeForestZone_20261025_024912_575.unity`: สำรองฉากรวมงานที่ยังไม่ได้ Save ก่อนจัดโซน หากต้องย้อนกลับ ใช้สำเนาฉากนี้ร่วมกับ TerrainData ต้นฉบับ ไม่เปิดทับงานปัจจุบันโดยไม่สำรอง
- ภาพตรวจใน `Logs/ForestDemo_Overview.png`, `ForestDemo_CloseUp.png` เป็นกล้องตรวจภาพชั่วคราว ไม่ได้เปลี่ยนมุมกล้อง gameplay; `ForestDemo_Gameplay.png` และ `ForestDemo_Combat.png` มาจาก Play Mode จริง

## ตรวจสอบ

- **Tools > World > Validate Forest Demo** → `Logs/ForestZoneValidation.txt`: ความสูงพื้นเดิม, Collider, หญ้า, Shader, Player และเส้นทางครบทุก SpawnPoint
- **Tools > World > Run Forest Playtest** → `Logs/ForestZonePlaytest.txt`: ใช้ Player/Input/Enemy/NavMesh จริง ทดสอบเดิน, Dash, Shadow Dash, ยิง และ Enemy ไล่บนพื้นสูง หลังทดสอบออกจาก Play Mode เอง ไม่ Save วัตถุทดสอบลงฉาก

ผลวันที่ 25 ตุลาคม 2026: การตรวจโซนผ่าน 24 ข้อ, Playtest บนพื้นจริงผ่าน 23 ข้อ และทวนระบบ tracer / Spiky Hit / distortion / เสียงปืน / inventory pause อีก 30 ข้อผ่านทั้งหมด (`Logs/RangedVfxRegression.txt`) C# build ไม่มี warning/error และ Console ไม่มี error หลังคอมไพล์รอบสุดท้าย

ยังไม่ได้ทำโลกนอกโซนให้เต็ม ไม่ได้ทำระบบจางใบไม้เมื่อบังกล้อง และยังไม่ได้ benchmark เครื่องเป้าหมาย ควรทดสอบประสิทธิภาพ/การบังกล้องอีกครั้งเมื่อขยายป่าหรือเพิ่มศัตรูจำนวนมาก
