# เมืองที่ย้ายได้ทั้งชุด

Scene: `Assets/_Project/Scenes/WorldPrototype.unity`

Parent ใน Hierarchy: `World > Map_WindingForest > City_All`

## วิธีใช้งาน

1. ออกจาก Play Mode แล้วเลือก `City_All` เพื่อย้ายตำแหน่ง X/Z หรือหมุนรอบแกน Y ทั้งเมือง
2. `City Assembly Root` จะปรับความสูงให้เข้ากับ Terrain อัตโนมัติใน Editor
3. ก่อนเข้า Play ระบบจะปรับพื้นใต้เมืองและทางเข้าประตู พร้อมสร้าง NavMesh ใหม่เมื่อเมืองถูกย้าย หากต้องการทำทันที ใช้เมนูของคอมโพเนนต์ `Ground Whole City and Rebuild Navigation`
4. บันทึก Scene หลังย้ายเสร็จ หากต้องการกำหนดความสูงเอง ให้ปิด `Auto Ground In Editor` และ `Level Terrain Pad`

ในชุดนี้รวมกำแพง อาคาร หลังคา ร้านค้า พร็อพ พื้น ถนน พื้นที่ก่อสร้าง จุดเดิน NPC คนงาน NPC ร้านอาวุธ และโซนเพลงเมืองไว้แล้ว ขอบเขต NPC งานก่อสร้าง และเพลงอ้างอิงตำแหน่งของ parent จึงตามไปด้วยเมื่อย้ายหรือหมุนเมือง

## การวางติดพื้น

- ตำแหน่งเริ่มต้นของ `City_All` ประมาณ `(75.763, 4.000, -72.775)` ยังอยู่ที่ X/Z เดิม
- ฐานเมืองอยู่ระดับประมาณ 4 เมตร และผิวพื้นเดินประมาณ 4.14 เมตร มีฐานหินรองขอบพื้น
- Terrain ที่ใช้งานเป็นสำเนา `Assets/_Project/World/CityAssembly/MovableCityTerrain.asset` ปรับความสูงเฉพาะใต้เมืองและทางเข้าประตู พร้อมไล่ระดับขอบ 3 เมตร
- คงข้อมูล texture ต้นไม้ และหญ้าไว้ การตรวจสอบพบว่าความสูง Terrain นอกขอบเขตที่ปรับเปลี่ยนไป 0 เมตร
- ถนนป่าของแผนที่หลักเป็นส่วนของแผนที่ หากย้ายเมืองไปไกลต้องจัดแนวทางเชื่อมเข้าประตูใหม่

## การตรวจสอบ

- `Logs/CityAssemblyFinalValidation.txt`: ตรวจ parent ขอบเขตเพลง NPC จุดงาน และทดลองย้าย/หมุนแล้วคืนตำแหน่งเดิม
- `Logs/CityGrounding.txt`: ขอบเขตการปรับ Terrain
- `Logs/CityAssemblyPlaytest.txt`: ทดสอบผู้เล่นเดินผ่านประตู เมืองมี NPC ครบ 12 ตัว การเดินของ NPC และเพลงเข้า/ออกเมือง
- `Logs/CityAssembly_Gameplay.png`: ภาพเมืองขณะทดสอบ
- สำรอง Scene ก่อนจัดกลุ่ม: `Logs/SceneBackups/WorldPrototype_BeforeCityParent_20261027_130413.unity`
