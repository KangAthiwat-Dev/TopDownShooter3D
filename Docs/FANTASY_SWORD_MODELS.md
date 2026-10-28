# โมเดลดาบสำหรับระบบร้านอาวุธ

ใช้ดาบจากแพ็ก **45 FREE Fantasy Weapon Props** ของ Kendall H ซึ่งนำเข้า Unity ในโฟลเดอร์ `Assets/Kendall H/Easy Weapons`.

## ไฟล์พร้อมใช้

- `Assets/_Project/Prefabs/Weapons/FantasySwords/Sword_01.prefab` ถึง `Sword_05.prefab`
- มีดาบ 5 ทรง ใช้สีสำเร็จของแพ็กเดิมและ Material ที่รองรับ URP
- ยาวประมาณ 1.10 เมตรทุกทรง เพื่อใช้กับตัวละครขนาดปัจจุบัน
- แกน +Y ชี้ไปปลายดาบ แกน X ตามความกว้างใบดาบ จุดกำเนิดอยู่กลางด้าม
- Root scale เป็น 1; ปรับสเกลและแกนที่ child `Model`
- เป็นโมเดลอย่างเดียว ไม่มีสคริปต์ตัวอย่าง Collider หรือ Rigidbody
- ส่วน `Enchantment` เก็บไว้แต่ปิดเริ่มต้น เปิดจาก child ของ Model ได้ภายหลัง
- อ้าง Mesh ร่วมกับต้นฉบับ ไม่ทำสำเนาข้อมูล Mesh เพิ่ม

โมเดลเหล่านี้ไม่ถูกแจกให้ Player ตอนเริ่มเกม.
ขณะนี้เชื่อมข้อมูลชื่อ ราคา Damage ไอคอน และ `MeleeWeaponDefinition` กับร้านดาบแล้ว ดู [ระบบร้านพ่อค้าอาวุธ](WEAPON_MERCHANT_SHOP.md).

## ต้นฉบับที่เก็บไว้

ใน `Assets/Kendall H/Easy Weapons` เหลือ:

- ดาบ Regular 5 Prefab พร้อม Mesh สีสำเร็จ
- ดาบ RGBA 5 Prefab พร้อม Mesh ที่แบ่งช่องสีไว้สำหรับแต่งสีภายหลัง
- Material, Shader Graph และ Texture ที่ดาบต้องใช้

RGBA เป็นข้อมูลช่องสีสำหรับทำเครื่องมือแต่งสีต่อ ไม่ใช่สีสำเร็จสำหรับใช้แทน Regular โดยตรง.
ถอด `weaponControllerAA` ออกจาก Prefab ก่อนลบสคริปต์ จึงไม่เกิด Missing Script.
แก้ Material ที่เดิมอ้าง Texture GUID ซึ่งไม่มีในโปรเจกต์ ให้ใช้ `roughnessLowScale 1.png` ที่มากับแพ็ก และล้างช่อง Normal เก่าที่ไม่ใช้.

ลบอาวุธชนิดอื่นทั้งหมดในแพ็ก รวมถึงธนู หน้าไม้ ลูกธนู ปืน ขวาน ค้อน โล่ และไม้เท้า พร้อม Mesh, Scene, สคริปต์และไฟล์ตัวอย่างที่ไม่ได้ใช้.
ตรวจแล้วว่าไม่มี Scene หรือ Prefab นอกแพ็กอ้างไฟล์ที่ลบ.

ขนาดไฟล์ Asset ต้นฉบับลดจากประมาณ **465 MiB เหลือ 62 MiB** หรือลดประมาณ **87%** (ไม่นับ Library และไฟล์ .meta).
Unity จะจัดการแคชของไฟล์ที่ลบเมื่อรีเฟรชโปรเจกต์.
สำเนาบีบอัดของไฟล์ที่ลบอยู่ใน `Logs/AssetBackups/FantasyWeapons_non_swords_20261028.zip` ซึ่งอยู่นอก Assets และถูก Git ignore.
ไม่ได้แก้ dependency ของ Package Manager ที่ผู้ใช้ติดตั้งไว้.

## เปิดดูและตรวจซ้ำ

เลือก `Tools > Weapons > Prepare Fantasy Sword Models` ใน Edit Mode.
เมนูจะสร้างเฉพาะ Prefab ที่ยังไม่มี จึงไม่เขียนทับการปรับแต่ง Prefab เดิม.
จากนั้นตรวจ Mesh, Material/Shader, Missing Script, ขนาดและ Component พร้อมสร้างภาพตัวอย่าง.

- รายงาน: `Logs/FantasySwordModels.txt`
- รายงานการลดขนาดแพ็ก: `Logs/FantasySwordCleanup.json`
- ภาพดาบเรียง Sword 01–05 จากซ้ายไปขวา: `Logs/FantasySwords_Preview.png`

ดาบทรง 04 มี Mesh ละเอียดกว่าแบบอื่น เหมาะกับอาวุธ Player หรือสินค้าที่แสดงจำนวนน้อย.
การเตรียมครั้งนี้ยังคงรายละเอียดเดิมของโมเดล ไม่ได้ลดจำนวน Polygon.
