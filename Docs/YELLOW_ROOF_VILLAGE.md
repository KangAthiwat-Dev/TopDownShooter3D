# หมู่บ้านหลังคาฟางสีเหลือง

Scene: `Assets/_Project/Scenes/WorldPrototype.unity`

Parent: `World > Map > Zone_Villager > Village_YellowRoof_Farm`

## สิ่งที่จัดไว้

- บ้านประกอบจากกำแพง มุม และหลังคา 7 หลัง ใช้หลังคา `RoofThatchS` สีเหลืองจาก Medieval Village Kit และปรับสเกลตามขนาดผู้เล่น
- คอกแกะ/แพะ คอกวัว เพิงหลังคาเหลือง รั้วไม้ ช่องประตูเปิด รางน้ำ ฟาง และเล้าไก่
- แปลงกะหล่ำ แครอท ฟักทอง ข้าวสาลี และมุมสมุนไพร พร้อมหุ่นไล่กาและรถเข็น
- ลานบ่อน้ำ ทางดินโค้งขอบจาง เตาอบ ผ้าตาก ไม้ฟืน ต้นไม้ และพุ่มไม้
- กังหันลมที่ผู้ใช้ประกอบไว้ถูกนำกลับมาใช้ ลดขนาด และเปลี่ยนส่วนหลังคาเป็นโทนฟาง

พื้นที่หลักอยู่ประมาณ X -90 ถึง -40 และ Z 37 ถึง 94 บนพื้นเดิมระดับประมาณ 4 เมตร จัดอยู่ฝั่งหมู่บ้านก่อนถึงลำธารด้านหน้า เก็บระดับ Terrain ลำธารตื้น และหุบลึกส่วนอื่นไว้เหมือนเดิม ไม่มีการเปลี่ยน texture ของ Terrain

## ชาวบ้าน

มีประชากรสูงสุด 10 คน ชาย 5 หญิง 5 แยก manager และ prefab จากเมือง ใช้ชุด Peasant โทนดิน เทา ครีม และเขียวหม่น ด้วย texture `T_Peasant_2_BaseColor` และ material สำเนาในโฟลเดอร์หมู่บ้าน จึงไม่เปลี่ยนชุด NPC เมือง

ใช้ระบบ `TownNpcPopulation` / `TownNpcBrain` เดิม: เดิน สุ่มวิ่งเล็กน้อย สนทนาด้วย `Idle_Talking_Loop` และทำท่า `Interact` ตามจุดกิจกรรม หลบ enemy และเกิดทดแทนตามจำนวนสูงสุดเมื่อมีคนตาย กำหนด `Max Outside` เป็น 0 เพื่อให้กิจวัตรอยู่ในหมู่บ้าน

จุดเดินแก้ได้ใต้ `07_Village_NPC_Population/Editable_Village_Activities` ส่วน `Tilled_Ground_NPC_Exclusion` กันเส้นทาง NavMesh ไม่ให้เดินลัดแปลงผัก สัตว์ในคอกเป็นโมเดลตกแต่งของ asset ยังไม่มีระบบเลี้ยงสัตว์หรือ AI สัตว์เลี้ยง

## ปรับแต่งต่อ

- บ้าน กำแพง หลังคา รั้ว และพร็อพเป็นชิ้นแยก สามารถย้าย/ปรับสเกลได้ใน Hierarchy
- วัตถุร่างเดิมอยู่ใน `00_Original_Sketch_Preserved_Inactive` เก็บไว้แบบ inactive
- เมื่อย้ายอาคาร รั้ว หรือจุดเดิน ให้ Bake `Navigation` ใหม่ก่อนทดสอบ
- เครื่องมือและการตรวจสอบอยู่ที่ `Tools > World > Yellow Roof Village` คำสั่ง Build ใช้ครั้งเดียวเพื่อไม่ทับงานตกแต่งภายหลัง

Assets ที่สร้างเพิ่ม: `Assets/_Project/World/YellowRoofVillage`

## หลักฐานการตรวจสอบ

- `Logs/VillageBuildScope.txt`: เทียบ 4,611 คอมโพเนนต์นอกพื้นที่หมู่บ้านและค่า Terrain heights ก่อน/หลัง
- `Logs/VillageValidation.txt`: บ้านอยู่ในเขต วางติดพื้น วัสดุ URP และเส้นทางกิจกรรมเชื่อมครบ
- `Logs/VillagePlaytest.txt`: ผู้เล่นเดินจริง ชาวบ้านครบ แอนิเมชัน สนทนา Interact และเกิดทดแทนโดยไม่กระทบประชากรเมือง
- `Logs/YellowVillage_Overview.png`, `Logs/YellowVillage_Gameplay.png`, `Logs/YellowVillage_Villagers.png`
- สำรอง Scene ก่อนเริ่ม: `Logs/SceneBackups/WorldPrototype_BeforeYellowVillage_20261027_151511.unity`
