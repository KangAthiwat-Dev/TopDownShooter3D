# Summoning Sanctuary — แท่นอัญเชิญและวงเวทเกิด

`WorldPrototype` มีแท่น จุดเกิด Player และวงเวทเกิดจาก Magic Effects FREE แล้ว วงเวทค่อย ๆ สว่างขึ้นและจางหายประมาณ 3.85 วินาที เล่นใหม่เมื่อเข้า/โหลดฉากใหม่ ยังไม่ซ่อน/ปรากฏร่าง Player ไม่ล็อกควบคุม ไม่เปลี่ยน Monster/Wave และยังไม่ทำระบบกันตกขอบแมป

## เปิดดูและลองเล่น

- เปิดฉาก `Assets/_Project/Scenes/WorldPrototype.unity`
- ใช้ **Tools > World > Summoning > Focus Platform** แล้วกด Play
- โซนอยู่ใต้ `World/Zone_ForestDemo/Temple/Zone_SummoningSanctum`
- Player เริ่มกลางแท่นที่โลกประมาณ **(-82.63, 5.778, -82.18)** หันไปทางตะวันออกเฉียงเหนือเข้าสู่ป่า

นำ `CirclePlatformSmall_001` ที่ผู้ใช้วางไว้มาใช้ต่อ คงตำแหน่งและสเกลโลกเดิม **(-82.63, 4.95, -82.18), scale (1, 0.5, 1)** ไม่สร้างแท่นซ้ำกลางป่า คงรูปปั้นและเสาเดิมไว้ เพิ่มเสาสมบูรณ์/เสาหัก กำแพงพัง และทางแผ่นหินจากแพ็ก `Columns ancient  ruins` ที่นำเข้าแล้ว โดยทำวัสดุ URP ของโครงการแยก ไม่แก้ prefab/texture ต้นฉบับของทั้งสองแพ็ก

รอยสลักกลางแท่นเป็น Mesh สีหินเข้ม **ไม่เรืองแสง** ไม่เพิ่ม Collider มาขวางเท้า ทางลาดรอบแท่นเดิมและช่องออกตะวันออกเฉียงเหนือเดินขึ้นลงได้

## ย้ายจุดเกิด

1. เลือก `05_SpawnAndFutureVfx/PlayerSpawnPoint` ใต้โซน
2. ย้าย/หมุน Marker ให้เท้า Player อยู่สูงกว่าผิวที่ต้องยืนประมาณ **0.08m** หลีกเลี่ยงยอดประดับและขอบแท่น
3. `PlayerSceneSpawnPoint` เชื่อม Player ตัวเดิมไว้แล้ว และย้าย Player ก่อน `PlayerMovement.Awake` / กล้องเริ่มตาม ไม่สร้าง Player ใหม่
4. Save ฉาก แล้วเริ่ม Play หรือ Restart เพื่อตรวจตำแหน่งใหม่

โค้ดอยู่ที่ `Assets/_Project/Scripts/Core/PlayerSceneSpawnPoint.cs` ใช้ `PlacePlayer()` วางตัวละครโดยปิด/คืน CharacterController ชั่วคราว ไม่มีการ reset HP, Inventory, Energy หรือระบบ combat และไม่ใช่ระบบ checkpoint/respawn ระหว่างเล่น

`SummonVfxAnchor` อยู่ตรงผิวกลางแท่น และมีลูก `PlayerSummonCircle` (Prefab) สำหรับระยะที่ 2 ผูกกับตำแหน่งนี้โดยไม่ต้องเดาพิกัดใหม่ `PlacePlayer()` เป็นเพียง utility ย้าย Player ไม่สร้างวงเวทซ้ำ

## ปรับวงเวทเกิด — ระยะที่ 2

- เลือก `05_SpawnAndFutureVfx/SummonVfxAnchor/PlayerSummonCircle` แล้วปรับ component `SummoningCircleVfx`
- `Effect Color` สีฟ้าเริ่มต้น; `Radius` รัศมีโลก 2.3m; `Brightness` ความสว่างเริ่มต้น 2
- `Fade In Duration` 0.65s → `Hold Duration` 2s → `Fade Out Duration` 1.2s ปรับแยกได้ทั้งหมด
- `Light Intensity`/`Light Range` คุมไฟฟ้าที่พื้น ตั้ง Intensity เป็น 0 เพื่อปิดไฟ โดยไม่ดับลายวงเวท
- `Authored Radius` เป็นค่าขนาดอ้างอิงของต้นฉบับ (1.5m) ไม่ต้องแก้เพื่อย่อ/ขยาย ให้ใช้ `Radius` แทน
- สร้างเป็นสำเนาจาก `Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab` ซึ่งมีอักษรรูน วงแหวน เส้นพลัง และประกายตามภาพอ้างอิง เก็บต้นฉบับไว้ ใช้วัสดุ/Shader URP ของโปรเจกต์แยกภายใต้ `MagicCircle/`
- Prefab ใช้ต่อได้ที่ `MagicCircle/PlayerSummonCircle.prefab` (เช่น Instantiate สำหรับ Monster ภายหลัง); แต่ตอนนี้ติดตั้งเฉพาะ Player บนแท่น ไม่เปลี่ยนการเกิดศัตรู
- สี/ความสว่าง/fade ใช้ MaterialPropertyBlock ต่อ instance ไม่แก้ shared material จึงใช้หลายสีพร้อมกันได้ ปล่อย Particle ให้เคลื่อนไหวตามต้นฉบับ แต่ควบคุมการเล่นให้จบครั้งเดียว
- ไม่มี Collider/การโจมตี/trigger และใช้ Ignore Raycast; เกม Pause แล้วหยุดอนุภาค fade และเวลาลบพร้อมกัน พอจบหรือถูกปิดระหว่างเล่นจะลบทั้งเอฟเฟกต์/ไฟ ไม่มีของค้าง ไม่ใช่ pooled object
- ดูภาพตัวอย่างด้วย **Tools > World > Summoning > Capture Magic Circle** → `Logs/SummoningCircle_Preview.png` (ไม่ Save ของจำลองลงฉาก)

## สิ่งที่เปลี่ยนและสิ่งที่รักษาไว้

- จุดเกิดศัตรูเดิมทั้ง 6 จุดย้ายมาอยู่ใกล้แท่น ห่าง Player ประมาณ 11.5m เพื่อให้ Wave เดิมยังตรวจเจอ Player อบ NavMesh ใหม่เป็น `SummoningNavigation.asset` เก็บ baked asset เก่าไว้
- รักษา wave definitions, จำนวนศัตรู, delay, damage, ความเร็ว Player, Dash, ปืน, เสียง และ Inventory เดิม
- ไม่ปั้นพื้น ไม่ทาสีพื้นใหม่ ไม่เติม/ลบหญ้า ไม่ย้ายต้นไม้หรือรูปปั้น ไฟตกแต่งของผู้ใช้ยังอยู่ วงเวทมีไฟพื้นชั่วคราวหนึ่งดวงที่จางและถูกลบพร้อมเอฟเฟกต์
- วัสดุเฉพาะโซนอยู่ใน `Materials/` ปรับสี/ความเงาของเสาใหม่โดยแก้ไฟล์เหล่านี้ ไม่แก้วัสดุในแพ็ก

## ข้อมูลสำรองและประเด็น Terrain ของ Unity

- ฉากรวมงานที่ยังไม่ได้บันทึกก่อนจัดแท่น: `Logs/SceneBackups/WorldPrototype_BeforeSummoning_20261025_164824_393.unity` และสำเนารอบเริ่มตรวจ `*163833_988.unity`
- `Terrain_BeforeSummoning.asset`: สำเนาตรวจ heightmap และรายละเอียดหญ้าก่อนทำแท่น ทั้งสองส่วนเทียบแล้วตรงกันทุกจุด แต่การ clone TerrainData ของ Unity รอบแรกไม่รวมสีพื้นที่ยังค้างบน GPU จึง **ไม่ควรใช้สำเนานี้กู้สีพื้น**
- `Terrain_UserPaintBackup.asset`: สำเนาครบพร้อมสีพื้นล่าสุดจาก GPU อ่านข้อมูลสีโดยตรงก่อน synchronize แล้วเทียบก่อน/หลัง copy, `SyncTexture` และ Save ได้ตรงกันทุกค่า ไม่มีการทาใหม่บน Terrain จริง
- สำเนาไฟล์ Terrain บนดิสก์ก่อน synchronize อยู่ใน `Logs/SceneBackups/Terrain_BeforeGpuPaintSync_20261025_165944_694.asset` พร้อม `.meta`; ไฟล์ต้นทางยังเป็นฉบับ 07:30 ก่อนทำแท่น ยืนยันว่า installer ไม่ได้เขียนทับ Terrain ระหว่างสร้างโซน
- รายงานกระบวนการเก็บข้อมูลสี: `Logs/SummoningTerrainPaintPreservation.txt`

การเปิดฉากสำรองใช้ TerrainData ที่ฉากอ้างอิงอยู่ด้วย หากจะย้อนงานหรือสลับ TerrainData ควรสำรองสถานะล่าสุดก่อน อย่าวาง snapshot เก่าทับงานทาสีที่แก้เพิ่มภายหลัง

## การตรวจสอบ

- **Tools > World > Summoning > Validate Platform** → `Logs/SummoningValidation.txt`: จุดเกิดอยู่บนแท่น, Terrain sculpt/grass, สีพื้นกับสำเนาที่ synchronize แล้ว, Shader, Collider และเส้นทางจาก SpawnPoint ทั้ง 6 จุด
- **Run Platform Playtest** → `Logs/SummoningPlaytest.txt`: ผ่าน **24 ข้อ** ด้วย Player/Input/NavMesh/Enemy จริง ครอบคลุมเดินออกและขึ้นแท่นกลับ, Short Dash, Shadow Dash, Enemy ไล่, กระสุนทำ damage, Inventory pause และโหลดฉากจริงใหม่แล้วกลับมาเกิดที่เดิม
- **Validate Magic Circle** → `Logs/SummoningCircleValidation.txt`: วัสดุ URP ของโปรเจกต์, ไม่มี Collider/missing script และผูกกับ anchor ถูกต้อง
- **Run Magic Circle Playtest** → `Logs/SummoningCirclePlaytest.txt`: ผ่าน **22 ข้อ** ในฉากจริง ครอบคลุมเริ่มอัตโนมัติ/fade-in/out, ตัว Player ไม่ถูกล็อก, Particle/ไฟ, Pause, ปรับสี/ขนาดสอง instance แยกกัน, ยกเลิกแล้วลบครบ, จบแล้วไม่ค้าง และโหลดฉากจริงใหม่ได้เอฟเฟกต์หนึ่งชุด
- ภาพรวม: `Logs/SummoningPlatform_Overview.png`; ภาพกล้องเล่นจริง: `SummoningPlatform_Gameplay.png`, `SummoningPlatform_Combat.png`, `SummoningPlatform_SpawnAfterReload.png`

วัตถุทดสอบมีเฉพาะใน Play Mode ไม่ถูก Save ลงฉาก ปรับตัวตรวจ Forest เดิมให้รองรับพื้นจาก Collider บนแท่นด้วย แทนการถือว่า Player ต้องยืนบน Terrain โดยตรงเสมอ
