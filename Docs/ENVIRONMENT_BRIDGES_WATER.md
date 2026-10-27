# สะพานและน้ำใน Environment

- สะพานอยู่ที่ `World/Environment/Aligned_Bridges` แยกเป็น `brige` และ `brige_wood`
- จัดชิ้นส่วนตามแกนสะพาน ลดปัญหาการบิดจาก scale ของ parent และใส่ MeshCollider ให้ตรงโมเดล พร้อม Bake NavMesh ใหม่
- คงแนวและระดับน้ำที่ผู้ใช้วางไว้ จัดแผ่นน้ำสองชิ้นให้ต่อกันพอดี ปิด collider ของน้ำเพื่อไม่เป็นพื้นเดินล่องหน
- วัสดุน้ำ: `Assets/_Project/World/EnvironmentBridges/Environment_Gentle_River.mat` มีคลื่นเล็กและเงาสะท้อนเคลื่อนไหวใน shader ใช้พิกัดโลกเพื่อให้ข้ามรอยต่อได้ต่อเนื่อง
- ปรับ `Ripple height` (เริ่มต้น 0.018 เมตร), `Ripple speed` (0.45) และ `Ripple reflections` (0.12) ใน material ได้ ไม่ต้องเพิ่มระบบจำลองน้ำ
- ทดสอบผู้เล่นเดินข้ามสะพานทั้งสองไปกลับและเส้นทาง NavMesh ผ่านครบ: `Logs/EnvironmentBridgePlaytest.txt`
- Scene สำรองก่อนแก้: `Logs/SceneBackups/WorldPrototype_BeforeEnvironmentBridges_20261027_162733.unity`
