# กิลด์รับซื้อของจากมอนสเตอร์และ Gold

ฉาก: `Assets/_Project/Scenes/WorldPrototype.unity`

## วิธีเล่น

1. เดินเข้าหา NPC ผู้หญิงหน้าอาคาร `Gild` ให้มีข้อความ `[E] คุยกับเจ้าหน้าที่กิลด์`
2. กด E เปิดบทสนทนา แล้วใช้เมาส์เลือกคำตอบ
3. เลือกขายเพื่อดูวัตถุดิบ 8 ช่องจากกระเป๋าจริง เลือกไอเทม จำนวนด้วยปุ่ม − / + หรือเลือกทั้งกอง
4. ตรวจราคาต่อชิ้นและยอด Gold ที่จะได้รับ แล้วกดขาย
5. ย้อนกลับไปสนทนา หรือเลือก “ไม่มีอะไรแล้ว” เพื่อเล่นต่อ กด Esc หรือ X เพื่อออกได้เช่นกัน

ใช้ข้อความตามที่ระบุ:

- “สวัสดีนักภจญภัย มีอะไรให้ข้าช่วยมั้ย”
- “ฉันอยากขาย item จากการกำจัด monster”
- “ไม่มีอะไรแล้ว”

ระหว่างสนทนา/เลือกขาย เกมหยุดและแสดงเมาส์เพื่อให้เลือกได้สะดวก เปิดกระเป๋าหรือหน้าอัปเกรดซ้อนไม่ได้
ถ้าตาย NPC ถูกปิด หรือผู้เล่นออกนอกระยะ บทสนทนาจะปิดโดยไม่ล้างเหตุผลหยุดเกมของระบบอื่น

ระบบเชื่อมเฉพาะ NPC ที่มี `CityServiceRole.GuildBuyer` เดิมหน้า Gild
Hotel กับ Potion Store ยังคง NPC บทบาทเดิม และไม่มีการย้ายอาคารหรือ NPC

## ราคารับซื้อเริ่มต้น

| ไอเทม | Gold ต่อชิ้น |
|---|---:|
| Monster Bone | 4 |
| Raw Meat | 5 |
| Monster Fang | 6 |
| Monster Claw | 8 |
| Beast Hide | 12 |
| Demon Horn | 15 |
| Demon Wing | 22 |
| Dragon Scale | 40 |

แก้ค่า `Sell Price` ใน ItemDefinition ใต้ `Assets/_Project/Items/MonsterLoot/Items/` ได้เลย
เฉพาะไอเทมประเภท Resource ที่ราคา > 0 จึงขายได้ ของ Quest, ของไม่มีราคา และอาวุธประชิดจะไม่ถูกขาย
ราคายึดตาม asset ตอนทำรายการ ไม่รับราคาที่ส่งมาจากข้อความ UI

## Gold และเซฟ

- เพิ่ม `PlayerGoldWallet` บน Player และ `PlayerGoldUI` บน HUD ใต้ปุ่มอัปเกรด
- เริ่มจาก 0 Gold เมื่อยังไม่มีเซฟ แสดงยอดใน HUD และหน้าขาย
- เมื่อขายสำเร็จจะหักจำนวนไอเทมและเพิ่ม Gold เป็นรายการเดียวกัน แล้วแจ้ง UI หลังทั้งสองสถานะอัปเดตแล้ว
- การขายกองหมดจะคืนช่องให้วัตถุดิบอื่นโดยอัตโนมัติ
- ตรวจ slot, ชนิดไอเทมที่เลือกไว้, จำนวน, ราคา และเพดาน Gold ก่อนทำรายการ
- กดซ้ำหลังขายหมดหรือยอด Gold เกิน 999,999,999 จะไม่กินของและไม่เพิ่มเงินซ้ำ
- บันทึก Gold ทันทีหลังรายการสำเร็จเป็น JSON version 1 ใน PlayerPrefs key `TopDownShooter3D.Gold.v1`
- Gold คงอยู่ข้ามการเริ่มฉาก/เปิดเกมใหม่ ส่วนกระเป๋ายังใช้สถานะรอบเล่นปัจจุบันตามระบบเดิม
- ไม่เปลี่ยนเซฟเลเวล/อัปเกรดและ EXP

## โครงสร้างและการต่อร้านค้าในอนาคต

- `GuildBuyerInteractable`: ตรวจ NPC บทบาท GuildBuyer ระยะ 2.5 เมตร สิ่งกีดขวาง และตัวผู้เล่น แล้วเปิดหน้าคุย
- `GuildTradeUI`: บทสนทนา 2 ตัวเลือก รายการช่องกระเป๋า จำนวนที่เลือก ราคา และข้อความขายสำเร็จ
- `PlayerInventory.TrySell(slot, expectedItem, amount, wallet)`: ตรวจและทำรายการขายโดยไม่ลบของก่อนตรวจเงิน
- `ItemDefinition.SellPrice` / `CanSell`: ราคารับซื้อและประเภทของที่ขายได้
- `PlayerGoldWallet.TryCredit(amount)`: เพิ่มเงินจากรางวัลในอนาคต
- `PlayerGoldWallet.TrySpend(amount)`: ตรวจและหักเงิน สำหรับการซื้อทั่วไปในอนาคต; ร้านดาบใช้ธุรกรรมร่วมกับช่องอาวุธ
- `PlayerGoldWallet.Changed`: อัปเดตยอดเงินใน UI
- `GamePauseReason.Dialogue`: ใช้ pause owner เดิม ป้องกันการยิง/แดชหรือเปิด modal ซ้อน

ขณะนี้ร้านดาบใช้กระเป๋าเงินเดียวกันแล้ว ดู [WEAPON_MERCHANT_SHOP.md](WEAPON_MERCHANT_SHOP.md).
Gold อาวุธประชิด 2 ช่อง และของในกระเป๋า 8 ช่องบันทึกร่วมกัน ร้าน Potion เชื่อมแล้ว ดู [POTION_SHOP_AND_USE.md](POTION_SHOP_AND_USE.md) ส่วนเมนูเซฟของ Hotel ยังไม่ได้เชื่อม.

## ฟอนต์ภาษาไทย

เพิ่ม Noto Sans Thai Looped จาก [Google Fonts](https://github.com/google/fonts/tree/main/ofl/notosansthailooped)
พร้อมไฟล์สิทธิ์ SIL Open Font License 1.1 ที่ `Assets/_Project/UI/Fonts/NotoSansThaiLooped/OFL.txt`
มี font asset สำหรับ TextMeshPro และ fallback ฟอนต์เดิมสำหรับ Latin/สัญลักษณ์
ปรับขนาดกล่องข้อความให้รองรับสระและวรรณยุกต์ไทย พร้อมราคา จำนวน และยอดเงินที่แสดงครบ

## การตรวจสอบ

เมนู `Tools > NPC > Guild Trade > Run E Mouse Sale Gold and Save Test`

ทดสอบกด E จริง และส่งสถานะเมาส์จริงผ่าน Input System เพื่อคลิกคำตอบ เลือกไอเทม ปรับจำนวน และขาย
ตรวจเงินและจำนวนของ HUD กระเป๋าเต็ม/ว่าง กดขายซ้ำ slot เปลี่ยน ของ Quest เพดาน Gold
ข้อมูลเซฟเสีย การบันทึก/โหลดใน wallet instance ใหม่ การปิดหน้าต่าง/disable/pause ซ้อน และการตายขณะขาย
ตัวทดสอบใช้ save key ชั่วคราวและคืน Gold เดิมของผู้ใช้โดยไม่เขียนทับเซฟจริง
ผ่านทั้งหมด 40 รายการ รวมการเลือกขายเมื่อวัตถุดิบเต็มทั้ง 8 ช่อง

- ผล: `Logs/GuildTradePlaytest.txt`
- ราคาและการติดตั้ง: `Logs/GuildTradeInstallation.txt`
- ภาพ: `Logs/GuildTrade_Dialogue.png`, `Logs/GuildTrade_Selling.png`
- สำรองฉากก่อนติดตั้ง: `Logs/SceneBackups/BeforeGuildTrade_*.unity`
