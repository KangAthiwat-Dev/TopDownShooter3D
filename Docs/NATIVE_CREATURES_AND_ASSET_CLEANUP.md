# Wolf Boss, Giant Spider และ NPC จอมเวทจำลอง

## Alpha Wolf

- ใช้โมเดล `01_Monster: Wolf Boss` ของ HATOGAME แทน alphaWolf/Primate_No7 เดิม
- Prefab สำหรับ gameplay ยังคง `Assets/_Project/Prefabs/Enemies/Enemy_AlphaWolf.prefab` และ Species ID `alpha_wolf` เพื่อไม่ให้จุดอ้างอิงประชากรป่าขาด
- ใช้ Idle, Walk, Run, attack1 และ die จากแพ็กเดียวกัน แยก controller และคลิปใช้งานไว้ใน `Assets/_Project/Enemies/StoryCreatures/WolfBoss/`
- ไม่ใช้การแปลงท่าจากมังกรแล้ว คลิปชั่วคราวและโมเดลเก่าถูกถอดออก
- มีหนึ่งช่องใน WorldMonsterDirector ประเภท Forest เกิดใหม่ตามเวลาระบบประชากรเดิม
- ค่าตั้งต้น: HP 160, EXP 180, เดิน 1.35 m/s, วิ่งไล่ 3.8 m/s, กัด 14 damage, cooldown 2.7 วินาที
- Generic rig และ NavMeshAgent ควบคุมการเดินเหมือนมอนสเตอร์เดิม ไม่ใช้ root motion ขยับ root ตัวละคร
- Material แปลงเป็น URP Lit พร้อม albedo และ normal ของแพ็ก
- ตอนนี้ใช้ drop table วัตถุดิบสัตว์เดิมเป็นฐาน ยังไม่ได้เพิ่ม Wolf Fang หรือระบบรับ/ส่งเควส Hunt the Alpha Wolf
- แพ็กนี้ไม่มีเสียง และยังไม่มีเสียงหมาป่าเฉพาะใน Audio/Mobs จึงเว้นไว้สำหรับเพิ่มภายหลัง

## Giant Spider Lite

- เปลี่ยนเฉพาะ visual ของ `Enemy_Spider.prefab` โดยคง GUID, AI, HP 32, damage 6, EXP, เสียง และ drop table เดิม
- ใช้ท่า idle_loop, walk_loop, run_loop, bite ของ rig ใหม่จริง ไม่ผูกคลิปแมงมุมเก่าเข้ากับกระดูกคนละชุด
- ตัว Lite ไม่มี death clip จึงทำคลิปหุบขา/ล้มตายชั่วคราวใน Editor แล้วเก็บเป็น `.anim` ไม่คำนวณ IK เพิ่มขณะเล่น
- ภาพพื้นผิวและ normal แยกจากข้อมูลที่ฝังใน GLB ของแพ็กเดียวกัน ตั้งขนาดนำเข้าไม่เกิน 2048
- ไฟล์ใช้งานอยู่ใน `Assets/_Project/Enemies/StoryCreatures/GiantSpider/` ส่วนต้นฉบับและ license อยู่ใน `Assets/Assets_Imported/Giant_Spider_Lite/`

## จอมเวทจำลอง

- `NPC_ElderWizard_Placeholder` ใกล้จุดอัญเชิญ ใช้ HumanMale ที่มีอยู่ เปลี่ยนสีผม/เคราเป็นขาว ชุดสีม่วงเข้ม และไม้เท้าประกอบชั่วคราว
- มี Humanoid Idle, หันมองผู้เล่น, collider และ NavMeshObstacle; ไม่ใช่มอนสเตอร์และไม่เป็นเป้าโจมตี
- StoryActor ID = `elder_wizard` แยกตัวตนออกจาก visual เพื่อเปลี่ยนโมเดลภายหลัง
- Prefab อยู่ใน `Assets/_Project/Story/NPCs/` และ material ของตนเองอยู่ใน `Assets/_Project/Story/Materials/`
- รอบนี้ยังไม่เล่นฉากมอบแผนที่/เสียชีวิตหรือบทสนทนาเปิดเรื่องอัตโนมัติ

## Asset ที่ลบและไฟล์ปืนที่เก็บ

- ลบ alphaWolf เดิมและคลิปทดลองที่แปลงจากมังกร
- ลบแพ็ก `Assets/Spiders` และชุด animation/material เก่าที่ไม่ได้ถูกใช้อีก
- ลบ `Assets/Assets_Imported/SciFi_Space_Soldier` และ material ตัวทหารที่ไม่ใช้แล้ว
- ย้ายเฉพาะ mesh ปืนที่ผู้เล่นใช้อยู่, texture D/N/M/E และ URP material ไป `Assets/_Project/Weapons/SpaceRifle/`
- ย้ายผ่าน AssetDatabase เพื่อคง GUID ทำให้มือจับปืน, muzzle, renderer และ prefab ผู้เล่นยังอ้างถึงไฟล์เดิมได้
- เก็บ animation ของผู้เล่นที่คัดลอกไว้ใน `_Project` และกำลังใช้งานอยู่ ไม่ต้องเก็บโมเดล/rig ทหารต้นฉบับไว้เพื่อเล่นคลิปเหล่านั้น
- ตรวจ GUID อ้างอิงของไฟล์ serialized ที่รอดอยู่ก่อนลบ และเก็บสำเนากู้คืนเฉพาะงานนี้ใน `Logs/AssetCleanupBackups/` ซึ่งไม่ใช่ asset ที่ Unity นำเข้า

รายงานการลบ: `Logs/RetiredAssetCleanup.txt` และ `Logs/RetiredAssetReferenceCheck.txt`

ผลการลบ: **232 ไฟล์ asset/meta รวมประมาณ 222.1 MiB ออกจาก Assets**; ชุดปืนที่ย้ายเก็บไว้ประมาณ 2.4 MiB สำเนากู้คืนแยกอยู่ใน Logs และไม่ถูกนำเข้าเป็น asset

## ตรวจการทำงาน

เมนู **Tools → Story Creatures → Run Native Creatures and Rifle Play Test** ตรวจ rig/animation binding, material, จอมเวทและพื้น, จำนวน Alpha Wolf และ respawn, เดิน/วิ่งไล่, damage ตามจังหวะ, การตาย/ล้างศพ และส่งปุ่ม J ผ่าน Input System เพื่อยืนยันว่าปืนยังยิงได้หลังลบแพ็กทหาร โดยไม่แก้ Gold/ไอเทม/อาวุธที่ผู้เล่นมี

รายงาน: `Logs/NativeCreaturePlaytest.txt`; ภาพ preview: `Logs/AlphaWolf_Idle.png`, `Logs/GiantSpider_Idle.png`, `Logs/ElderWizard_Placeholder.png`; ภาพทดสอบยิงจริง: `Logs/RetainedRifle_Play.png`

ผลการตรวจ: **ผ่าน 46 ข้อใน Unity Play Mode** ไม่มี runtime error ระหว่างตรวจ และ Gold/ไอเทม/อาวุธเดิมของผู้เล่นคงเดิม
