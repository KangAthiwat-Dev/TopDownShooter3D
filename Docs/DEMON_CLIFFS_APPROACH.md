# Demon Cliffs — outdoor castle approach

Scene: `Assets/_Project/Scenes/WorldPrototype.unity`

## เส้นทางและขอบเขตงาน

ทางป่าเข้าฝั่งซ้าย → ลานต่อสู้ชั้น 1 → สะพานโค้งอ้อมลาวาด้านล่าง → ทางขึ้นขวาล่าง → ลานชั้น 2 → ทางลาดขึ้นลานรองบอสบนสุด → จุดประตูปราสาทในอนาคต

| จุด | ตำแหน่ง X/Y/Z โดยประมาณ | หมายเหตุ |
| --- | --- | --- |
| Entrance | 8 / 4.17 / 49 | ต่อทางจากเมืองและหมู่บ้าน |
| Stage 1 | 33 / 9.8 / 49 | ลานต่อสู้แรก |
| Stage 2 | 69 / 14.8 / 54 | เข้าจากทางลาดขวาล่าง X84 |
| Stage 3 | 65 / 20.1 / 80 | ลานรองบอส |
| Castle entrance | 65 / 20.1 / 89 | จุดต่อ map ภายในภายหลัง |
| Castle footprint | 64 / 20.1 / 91 | เว้นพื้นที่ประมาณ 28 × 10 เมตร |

รอบนี้ทำเฉพาะพื้นที่ภายนอกและจุดอ้างอิง ยังไม่มีอาคารปราสาท, monster ประจำลาน, รองบอส, ระบบฆ่าครบเปิดประตู หรือการเปลี่ยน scene ภายในปราสาท จุด `GateAnchor_InactiveGameplay` เป็น Transform ว่าง ไม่กีดขวางผู้เล่น

## แก้ใน Unity

Hierarchy: `World/Map_WindingForest/DemonCliffs_CastleApproach`

- `00_UserSketch_Reference_Inactive`: เก็บแท่นและหินเดิม 8 ชิ้นพร้อม prefab link และ world transform เดิม ปิดไว้เป็น reference
- `01_Walkable_ThreeStages_And_Ramps`: พื้นแต่ละชั้น สะพานโค้ง และทางลาด MeshCollider
- `02_CliffEdge_Colliders`: collider ขอบกันตก รวมถึงขอบฝั่งลาวา ใช้ Environment layer 7 จึงกัน shadow dash ด้วย
- `03_Basalt_Scenery`: หน้าผา เสาหัก กระดูก เตาไฟ และขอบหิน
- `04_Lava_Basin_And_NotWalkable_Mask`: ลาวา Y4.25 และ NavMeshModifierVolume เฉพาะด้านล่าง ลาวาเป็นฉากหลัง ยังไม่มีระบบ damage/ตายเพราะลาวา
- `05_Encounter_And_Travel_Anchors_Future`: จุด spawn อนาคต, ศูนย์ลาน, gate, castle footprint และ waypoints
- `06_Local_Atmosphere_Audio_And_Braziers`: ไฟ ควันบาง ๆ แสงเฉพาะจุด local grading และเสียงลาวา 3D
- `07_Jagged_Rock_Details`: หินแหลมตกแต่งเพิ่มเติม

เลือก root แล้วดู `DemonCliffZone` เพื่อแก้จุดลาน จุด spawn หรือ footprint ผ่าน Inspector; Gizmos แสดงแนวทางและพื้นที่ต่อสู้ ข้อมูลนี้ยังไม่ควบคุม encounter

พื้น/ทางลาดเป็น mesh สร้างครั้งเดียว ย้ายและหมุน object ได้ แต่การย้ายพื้นต้องย้าย collider ขอบและ anchors ที่เกี่ยวข้องตามด้วย หลังปรับสิ่งกีดขวางให้ bake navigation ใหม่เป็น asset ใหม่ ห้ามเรียก Build ทับ — มี guard ป้องกันการเขียนทับงานแต่งภายหลัง

เมนู `Tools > World > Demon Cliffs` มี Focus Approach, Capture Clean Overview, Validate Navigation and Sources และ Run Actual Player and Bear Playtest

## Assets และการรักษางานเดิม

- ใช้โมเดล/ลาวาจาก `ithappy/Inferno_World_Free` ที่มีอยู่แล้ว Shader Graph ลาวา Unity 6 URP ผ่านการตรวจ support และ shader errors
- ใช้ไฟ/เสียงจาก Flipbook VFX Bundle Lite URP; ควันใช้ texture ของ Inferno กับ material URP ส่วนตัว
- Terrain clone จาก **GroundedCityTerrain รุ่นที่ผู้ใช้แก้ล่าสุดใน scene** เป็น `Assets/_Project/World/DemonCliffs/Terrain/DemonCliffsTerrain.asset`
- ลดพื้นเฉพาะ basin ลาวา; height samples นอก basin ไม่เปลี่ยน Painting/detail ตัดเฉพาะ basin และทางป่าเชื่อมเข้าใหม่
- TerrainCollider อ้างข้อมูลชุดเดียวกับ Terrain; bake เป็น `DemonCliffsNavigation.asset` โดยเก็บ asset navigation เดิม
- ต้นไม้/พุ่มใน basin หรือขวางทางเชื่อมถูกปิดเฉพาะจุด ไม่ลบต้นฉบับ
- ไม่มีการแก้ source material ของ pack, เมือง, NPC, จุดเกิด, อุปกรณ์, กล้อง, เพลงเมือง หรือระบบต่อสู้เดิม
- `AdventureHudUI` ตรวจตำแหน่งใน polygon และแสดง DEMON CLIFFS / CASTLE APPROACH
- `ForestMapLayout.castle` อ้าง landmark ที่ castle footprint ในโซน NE (ไม่ใช้ world origin); เมื่อมี `DemonCliffZone` จะใช้ polygon แทนรัศมีโซนปราสาทแบบเก่า

`DemonCliffAtmosphere` เปิดอนุภาคในระยะ 90 เมตรและ fade เสียงลาวาที่ volume 0.08 โดยเสียงมี 3D rolloff ถึง 65 เมตร เคารพ pause ของเกมและ AudioListener ไม่เปลี่ยน global lighting/fog/audio

## หลักฐานตรวจสอบ

- `Logs/DemonCliffsBuild.txt`: backup path, รายการ sketch/foliage, การคง terrain/navigation เดิม และ preservation audit ของ component เดิม
- `Logs/DemonCliffsValidation.txt`: การเชื่อม NavMesh ทุกจุด, monster anchors, ทางจากเมือง/หมู่บ้าน และ shader
- `Logs/DemonCliffsPlaytest.txt`: actual CharacterController ผ่านทุก waypoint, dash/sprint/shadow บนลาด, ขอบกันตก, pause/audio และ Bear NavMeshAgent
- `Logs/DemonCliffs_Overview.png`: ภาพรวม
- `Logs/DemonCliffs_Stage1_Gameplay.png`, `Stage2_Gameplay.png`, `Stage3_Gameplay.png`: ภาพจากกล้อง player ระหว่างทดสอบ

ก่อน build บันทึกสำเนา scene ปัจจุบันไว้ใน `Logs/SceneBackups/WorldPrototype_BeforeDemonCliffs_*.unity` รายละเอียดชื่อเต็มอยู่ใน build report
