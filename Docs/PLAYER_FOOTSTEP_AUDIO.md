# เสียงเดินผู้เล่น

ติดตั้งใน WorldPrototype วันที่ 28 ตุลาคม 2026

- พื้นที่รองรับเท้าเป็น `TerrainCollider`: ใช้ `Audio/Interaction/Movement/walk_sound_outside.mp3`
- พื้น Object เช่นถนน พื้นเมือง พื้นปราสาท สะพาน: ใช้ `walk_sound_inside.mp3` แม้มี Terrain อยู่ใต้ Object อีกชั้น
- ตรวจพื้นจริงใต้เท้า ใช้เสียงเฉพาะเมื่อผู้เล่นเคลื่อนที่ติดพื้น มี fade 0.1 วินาทีเมื่อหยุดหรือเปลี่ยนพื้น
- เพิ่มจังหวะเสียงตามความเร็ว หยุดเมื่อ Dash/แปลงร่าง ลอยตัว ตาย ล็อกการเคลื่อนที่ หรือเปิดเมนู
- ปรับความดังได้ที่ `Player > PlayerFootstepAudio > Volume` ค่าเริ่มต้น 0.24 เสียงเป็นแบบ 3D ระยะ 5–35 เมตร
- โค้ดหลัก: `Assets/_Project/Scripts/Player/PlayerFootstepAudio.cs` ไม่เปลี่ยนระบบ movement หรือ animation

ทดสอบ Play Mode ผ่าน 10 กรณี รวม Terrain, Object ที่วางเหนือ Terrain, หยุดเดิน, sprint, dash, pause และลอยตัว ไม่มี runtime error ผล: `Logs/PlayerFootstepPlaytest.txt`
