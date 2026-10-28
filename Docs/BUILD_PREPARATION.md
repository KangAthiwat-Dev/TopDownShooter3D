# เตรียม build Windows และ macOS

ชื่อเกมและชื่อไฟล์ executable: **EverwoodTheSummonedHero**

## ฉากที่ใช้

ใช้ Unity **6000.5.10f1** และติดตั้ง Windows Build Support กับ Mac Build Support

1. `Assets/_Project/Scenes/MainMenu.unity` เป็นฉากแรก
2. `Assets/_Project/Scenes/WorldPrototype.unity` เป็นฉากเกม

นำ SampleScene และ PrototypeArena ที่ไม่ได้ใช้งานออกจากรายการ build แล้ว

## สร้างไฟล์เกม

ออกจาก Play Mode แล้วเลือกเมนู **Tools > Build Preparation > Build Windows and macOS** หรือเลือก build เฉพาะระบบ

- Windows: `Builds/Windows/EverwoodTheSummonedHero.exe` — ส่งทั้งโฟลเดอร์ Windows รวมไฟล์ Data และ DLL
- macOS: `Builds/macOS/EverwoodTheSummonedHero.app`
- ผลรวม: `Logs/BuildPreparation/DesktopBuildResults.txt`
- รายละเอียด: `Logs/BuildPreparation/StandaloneOSX.txt` และ `StandaloneWindows64.txt`
- ขั้นตอนล่าสุด: `Logs/BuildPreparation/DesktopBuildProgress.txt`

ตัวช่วย build บันทึกฉาก ใช้รายการฉากข้างต้น และรอต่อหลัง Unity เปลี่ยน platform / reload scripts ได้ ผลสำเร็จต้องเป็น `Succeeded` ในรายงานของแต่ละระบบ

`Builds/` และ `Logs/` ถูก ignore ใน Git ให้นำไฟล์ build ไปส่งแยกจาก source project

## ผลตรวจวันที่ 28 ตุลาคม 2026

- macOS Universal (Apple Silicon และ Intel): **Succeeded, 0 errors**
- Windows x86-64: **Succeeded, 0 errors**
- เปิดโปรแกรม macOS ชุดสุดท้ายได้ และไม่พบ error/exception ใน Player.log ของรอบตรวจ
- ทดสอบใน Unity หลัง cleanup: pickup, hover, inventory, pause, กลับ MainMenu และเข้า WorldPrototype ที่จุดอัญเชิญ **ผ่านครบ** พร้อมคืนค่าเซฟเดิม
- หญ้า/น้ำพุ: การตรวจ exclusions และ collider ผ่าน
- แก้บอส 2 ตัวที่เริ่ม NavMeshAgent ก่อน NavMeshSurface ลงทะเบียน โดยเก็บ agent ในฉากเป็น disabled และให้ AI เปิดใน Start หลังหา NavMesh ที่ใช้ได้
- ยังมี warning จาก API เก่า, ฟิลด์ editor ที่ไม่ใช้ใน player และ shader ของ assets ที่นำเข้า รายละเอียดอยู่ในรายงาน build แต่ไม่มี build error
- ยังไม่ได้ทดสอบเปิดเกมจริงบนเครื่อง Windows

ผลทดสอบเมนู: `Logs/GameMenuPlaytest.txt` และข้อมูลไฟล์ที่ส่ง: `Builds/BUILD_INFO.txt`

## หญ้าและน้ำพุ

- เติมหญ้าเดิม `ForestGrass` บน terrain ที่ทาสีเขียว ประมาณ 19,691 กอ ใช้ mesh instancing และความหนาแน่นแปรตาม noise
- เว้นสิ่งปลูกสร้าง ถนน พร็อพ หิน โคนต้นไม้ น้ำ และพื้นที่ลาดชัน
- น้ำพุกลางเมืองมี MeshCollider บน layer Environment และ NavMeshObstacle แบบ carving
- ตรวจซ้ำได้ที่ **Tools > Build Preparation > Validate Grass Exclusions and Fountain**
- สำรองฉากและ terrain ก่อนแก้: `Logs/BuildPreparation/LandscapeBackup_20261028_172204/`

## Assets ที่นำออก

นำไฟล์ที่ไม่ได้ถูกอ้างอิงจากสองฉากจริงและระบบที่เก็บไว้จำนวน **5,866 ไฟล์ ประมาณ 2,546 MiB** ออกจาก Assets ทำให้ Assets เหลือประมาณ 1.5 GB

เก็บ dependencies จากฉาก, ProjectSettings, scripts/assemblies, shader libraries, Resources ของเกมและ TextMesh Pro, StreamingAssets, sprite atlas และไฟล์ licence รวมถึง avatar/material remap ที่อ้างอิงใน importer metadata

ตรวจแล้วไฟล์ที่เก็บไว้ไม่มี GUID อ้างถึงไฟล์ที่ย้ายออก รายการตรวจอยู่ใน `Logs/BuildPreparation/UnusedAssetPlan.json`

สำรองไฟล์ต้นฉบับและ `.meta` ไว้ที่:

`Logs/AssetCleanupBackups/BuildPreparation_20261028_175734/`

หากต้องการนำ asset ที่เคยนำเข้าแต่ยังไม่ได้ใช้กลับมา ให้ปิด Unity แล้วคัดลอกไฟล์/โฟลเดอร์นั้นจาก `Assets/` ภายใน backup กลับมายังตำแหน่งเดิมใน project **พร้อม `.meta`** เพื่อคง GUID เดิม แล้วเปิด Unity ใหม่ dependencies ของ asset นั้นอาจต้องนำกลับมาด้วย

หากต้องการคืนทุกไฟล์ ให้ **ผสาน (Merge)** โฟลเดอร์สำรองเข้ากับ Assets ปัจจุบันและเก็บไฟล์ปัจจุบันไว้ ใช้คำสั่งนี้จาก root project ขณะปิด Unity ได้:

```sh
rsync -a --ignore-existing Logs/AssetCleanupBackups/BuildPreparation_20261028_175734/Assets/ Assets/
```

โฟลเดอร์สำรองอยู่ใน Logs และไม่ถูก commit หรือรวมใน build
