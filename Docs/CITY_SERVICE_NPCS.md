# NPC ผู้หญิงประจำร้านในเมือง

วางใน Scene `Assets/_Project/Scenes/WorldPrototype.unity` เป็นลูกของอาคารที่ผู้ใช้ตั้งชื่อไว้โดยตรง จึงย้ายตามอาคารได้

| อาคาร | NPC | บทบาทสำหรับต่อระบบ | ชุด |
| --- | --- | --- | --- |
| Hotel | NPC_HotelSave_Female | เจ้าหน้าที่โรงแรม / จุดเซฟ | Amber โทนอุ่น ผมมวย |
| Gild | NPC_GuildBuyer_Female | เจ้าหน้าที่กิลด์รับซื้อของจากผู้เล่น | Plum สีม่วง ผมมวย |
| Potion_store | NPC_PotionStore_Female | พ่อค้าหญิงขาย potion | Teal สีเขียว ผมยาว |

`Potion_store` เป็นกลุ่มอาคารเดิม จึงเลือกหน้าประตู `04_Apothecary` ที่มีป้าย Potion เป็นจุดยืน ย้าย NPC Hotel ออกจากเงากันสาดของร้านตลาดข้าง ๆ ให้เห็นชัด

ทุกตัวใช้โมเดล Humanoid ผู้หญิงจาก TownDiversity พร้อม capsule collision และ NavMeshObstacle สำหรับให้ชาวเมืองเดินหลบ แยกจาก TownNpcPopulation และไม่มี TownNpcBrain, NavMeshAgent, CivilianHealth, EnemyHealth หรือ Targetable จึงไม่เดินสุ่มและไม่เป็นเป้าโจมตี ใช้ NpcIdleLook หันมองผู้เล่นใกล้ ๆ และ CityServiceNpc เปลี่ยนท่ายืนพัก/พูดคุย

Prefab และ controller อยู่ใน `Assets/_Project/NPCs/CityServices` ใช้คลิปยืนพัก Merchant_Idle และ Citizen_Idle_Talking_Loop ที่มีอยู่แล้ว

## จุดต่อระบบบริการ

`CityServiceNpc` เก็บ Role, Building และ InteractionPoint ไว้ให้เชื่อมต่อในขั้นต่อไป ตัว NPC มี `Service_Interaction_Point` เป็นจุดยืนของผู้เล่นด้านหน้า

เดิมเป็นการวาง NPC และพฤติกรรมต้อนรับ ต่อมาระบบขายไอเทมที่กิลด์และร้าน Potion เชื่อมกับปุ่ม E และ Gold แล้ว ดู [POTION_SHOP_AND_USE.md](POTION_SHOP_AND_USE.md) ส่วนเมนูเซฟผ่าน Hotel ยังไม่ได้เชื่อม ระบบ EXP เดิมบันทึกอัตโนมัติแยกอยู่แล้ว

ตรวจตำแหน่ง/rig/ช่องทางเดินและภาพได้จาก `Logs/CityServiceNpcPlacement.txt` และ `Logs/CityServices_*.png` รายงาน `Logs/CityServiceNpcScope.txt` เทียบองค์ประกอบเดิม 6,717 คอมโพเนนต์ พร้อมตำแหน่งและ parent เดิมระหว่างเพิ่ม NPC

## ระบบขายของที่ Gild

NPC GuildBuyer เชื่อมบทสนทนากด E และใช้เมาส์ขายวัตถุดิบมอนสเตอร์รับ Gold แล้ว
ดู [GUILD_SELLING_AND_GOLD.md](GUILD_SELLING_AND_GOLD.md) สำหรับราคา พฤติกรรม และจุดต่อร้านค้าในอนาคต
