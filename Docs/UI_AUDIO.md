# เสียง UI

ติดตั้งใน WorldPrototype วันที่ 28 ตุลาคม 2026

- เปิดหน้าต่าง: `Assets/_Project/Audio/UI/open_ui.mp3`
- ปิดหน้าต่าง: `close_ui.mp3`
- เมาส์ชี้ช่องไอเทม: `hover_slot.mp3`

ครอบคลุมหน้ากระเป๋า สกิล บันทึกเควส บทสนทนาทหารยาม และร้านกิลด์/ดาบ/ยา รวม 7 หน้าต่าง กับ hover 46 ช่องในกระเป๋า hotbar และร้านค้า ช่องว่างก็มีเสียง hover ได้ ไม่เปลี่ยนการเลือกหรือใช้ไอเทม

เสียงเป็น 2D และใช้ `ignoreListenerPause` จึงเล่นได้ขณะเมนูหยุดเกม แยกเสียงหน้าต่างกับ hover เป็นคนละ AudioSource เพื่อไม่ตัดเสียงกัน อยู่ค้างช่องเดิมจะไม่เล่นซ้ำ มี cooldown 0.075 วินาทีสำหรับการเลื่อนเมาส์เร็ว และเว้น hover 0.12 วินาทีหลังเปิด/ปิดหน้าต่าง การเปิดที่ถูกปฏิเสธ ปิดซ้ำ หรือสลับแท็บไม่เล่นเสียงเปิด/ปิด

ปรับความดังได้ที่ `HUDCanvas / UI Audio Feedback / UIAudioFeedback`: Window Volume เริ่มต้น 0.28, Hover Volume 0.18

โค้ด `UIAudioFeedback.cs` ดูแลเสียงร่วม; `UIModalAudio.cs` ตรวจการเปิด/ปิดจริง; `UISlotHoverAudio.cs` รับ pointer enter/exit จาก EventSystem โดยไม่ผูกกับการเลือกด้วยคีย์บอร์ด

ทดสอบผ่าน 16 กรณีใน Play Mode รวมเสียงขณะ pause, pointer events, cooldown ที่ timeScale=0, หน้าต่างจริง และป้องกันเสียงซ้ำ ไม่มี runtime error ผลอยู่ใน `Logs/UIAudioPlaytest.txt`
