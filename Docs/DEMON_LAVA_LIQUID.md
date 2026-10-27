# น้ำลาวาในภูมิประเทศที่ผู้ใช้ปรับใหม่

ใช้ `Assets/Shaders/Lava/Lava.mat` และ `Lavafall.mat` เป็นฐานของวัสดุสำเนาเฉพาะโซน

Hierarchy ใน `WorldPrototype`:

`World/Map_WindingForest/DemonLava_LiquidLake_And_ChannelStreams`

- `01_Liquid_Lake_Constant_Level`: **น้ำลาวาเต็มแอ่งกลาง** เป็นผิวน้ำระนาบระดับ Y2.35 ขอบ mesh ตัดตามระดับพื้นจริง พื้นที่ประมาณ 2,925 ตารางเมตร
- `02_Liquid_Streams_Inside_Painted_Channels`: ชั้นน้ำลาวาแยกจาก Terrain อยู่เฉพาะกลางร่องบนภูเขาที่ผู้ใช้วาดไว้ เว้นขอบ Magma สีดำเดิมไว้ทั้งสองข้าง

ไม่มีการเปลี่ยน texture, TerrainLayer, การทาสี หรือรูปทรง Terrain เดิม วัสดุ/mesh น้ำลาวาอยู่ใน `Assets/_Project/World/DemonLavaSurfaces/LiquidMaterials` และ `LiquidMeshes` ความเร็ว/สีปรับในวัสดุสำเนาได้

Shader เป็นสำเนาจาก asset ใหม่ เพิ่ม vertex mask สำหรับขอบน้ำจางเข้าฝั่ง ตัวไหลใช้ UV ตามความสูงและเลื่อนลง เนื่องจาก graph ต้นฉบับมี Power ของ UV.y จึงใช้ UV บวกเพื่อหลีกเลี่ยง NaN บน Metal

ชั้น overlay ที่สร้างผิดในรอบแรก `DemonLava_AnimatedPools_And_MountainFlows` ปิดไว้ ไม่แสดงในเกม งานปัจจุบันใช้ผิวน้ำสองชิ้นข้างต้น

น้ำลาวารอบนี้เป็นภาพและการไหล ยังไม่มี damage, buoyancy หรือการเปลี่ยน navigation/collider เมื่อแก้รูปร่าง/ระดับแอ่งภายหลัง ต้องปรับ mesh น้ำให้ตามขอบฝั่งใหม่ด้วย

หลักฐาน:

- `Logs/DemonLavaLiquidBuild.txt`: backup, ขนาดแอ่ง, การคง bytes ของ Terrain/asset ต้นฉบับ/navigation และ component เดิม
- `Logs/DemonLavaAnimation.txt`: ผลตรวจภาพเคลื่อนไหวใน Play Mode
- `Logs/DemonLava_Overview.png`: ภาพรวมหลังเติมน้ำลาวา
