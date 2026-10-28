# เมนูเกม เสียงเก็บของ และไฮไลต์ช่อง

ติดตั้งวันที่ 28 ตุลาคม 2026 ใน WorldPrototype และฉาก MainMenu ใหม่

## การใช้งาน

- `MainMenu.unity`: เข้าสู่การผจญภัย, วิธีเล่น, ออกจากเกม
- ในเกมกด **Esc** หรือปุ่ม **เมนูพักเกม**: เล่นต่อ, วิธีเล่น, กลับเมนูหลัก, ออกจากเกม
- ถ้าเปิดกระเป๋า ร้านค้า บทสนทนา หรือบันทึกเควสอยู่ Esc จะปิดหน้านั้นก่อน ไม่เปิด Pause ซ้อนในการกดครั้งเดียว
- กลับเมนูหลัก/ออกเกมมีหน้ายืนยันและบันทึก progression, Gold/ไอเทม/อาวุธ และ Story ก่อนออกจากฉาก
- เข้าเกมอีกครั้งจะเกิดที่จุดอัญเชิญพร้อมความคืบหน้าเดิม ไม่มีการลบเซฟหรือเริ่มตัวละครใหม่ ยังไม่ได้ทำ save ตำแหน่ง/checkpoint
- Build Settings เริ่มที่ MainMenu ตามด้วย WorldPrototype เก็บฉากเดิมอื่น ๆ ไว้; ใน Editor หากต้องการเริ่มจากหน้าเมนู ให้เปิด `Assets/_Project/Scenes/MainMenu.unity` แล้วกด Play

## Pickup และ Hover

- ใช้ `Audio/pickup_sound/freesound_community-item-pickup-37089.mp3` เมื่อของ drop เข้ากระเป๋าสำเร็จ และเมื่อเก็บอาวุธสำเร็จตรงจังหวะ animation
- ไม่เล่นเสียงเมื่อของเต็ม เก็บไม่สำเร็จ ซื้อจากร้าน หรือ restore เซฟ จำกัดเสียงเก็บของห่างกันอย่างน้อย 0.08 วินาทีเพื่อไม่ให้กอง loot ดังซ้อน
- ช่องที่มีเสียง hover ทั้ง 46 ช่องมีชั้นพื้นสีทองโปร่งแสง 10% แยกจากสี selected และ Button transition จึงไม่เปลี่ยนสีช่องที่เลือกไว้
- สีค่อย ๆ เปลี่ยนด้วย unscaled time ระหว่างเมนู pause ได้ กลับเป็นปกติเมื่อเมาส์ออกหรือช่องถูกซ่อน ไม่รับ raycast จึงไม่กั้นการกดไอเทม
- ปรับสีที่ `UISlotHoverAudio > Hover Tint`; ปรับเสียง pickup ที่ `PlayerPickupFeedback > Volume` เริ่มต้น 0.27

## จุดต่อโค้ด

| ไฟล์ | หน้าที่ |
| --- | --- |
| `Scripts/UI/GameMenuUI.cs` | หน้าเมนูหลักและ pause, วิธีเล่น, หน้ายืนยัน, โหลดฉากและ flush เซฟ |
| `Scripts/Core/GamePauseController.cs` | เพิ่มเหตุผล PauseMenu และจำ frame ที่ pause เปลี่ยนเพื่อจัดลำดับ Esc |
| `Scripts/Player/PlayerPickupFeedback.cs` | ฟัง MonsterLootWorld.Collected และ PlayerInteractor.WeaponCollected |
| `Scripts/Interaction/PlayerInteractor.cs` | ส่ง WeaponCollected เฉพาะหลัง TryCollect สำเร็จ |
| `Scripts/UI/UISlotHoverAudio.cs` | เสียง hover เดิมและชั้น highlight ใหม่ |
| `Editor/GameMenuSetup.cs` | สร้างหน้าต่าง/ฉากเมนูและตั้งลำดับ Build พร้อม backup WorldPrototype |

เมนูใช้ฟอนต์ไทยและภาพ parchment เดิม พื้นหลังหน้าเมนูใช้ภาพ overview ที่มีในโปรเจกต์ ไม่เพิ่ม asset pack หรือเปลี่ยนกล้องในเกม

## ทดสอบ

Tools → Game Menu → Test Pickup Hover Pause and Title Flow: เก็บของสำเร็จ/ของเต็ม, hover tint, Esc และเมนูซ้อน, help/confirmation/resume, โหลดกลับ MainMenu แล้วเข้า WorldPrototype จริง และตรวจข้อมูล inventory/Gold/อาวุธไม่หาย สำรอง PlayerPrefs ไว้ก่อนทดสอบและคืนหลัง Play Mode

ผล: `Logs/GameMenuPlaytest.txt`; ภาพ Full HD: `Logs/MenuTests/Main_Menu.png`, `Pause_Menu.png`, `Inventory_Hover.png`

ผลล่าสุด: ผ่าน 24 เงื่อนไขการเล่นและการเปลี่ยนฉาก พร้อมตรวจคืน PlayerPrefs เดิม รวม 25 รายการ ไม่มี runtime error ตรวจภาพเมนูหลัก เมนูพัก และ hover ที่ Full HD แล้ว
