# TopDownShooter3D — Implementation Handoff

อัปเดตจากสถานะของโปรเจกต์ ณ วันที่ 26 กันยายน 2026 อัปเดตระบบโจมตี การเดิน/วิ่ง กล้อง การเก็บอาวุธ และ HUD วันที่ 23 ตุลาคม 2026; Locomotion ของ Kevin Iglesias, HumanMale_Character_Free/รูปลักษณ์, การเล็งตามแนวลำกล้อง, Ice Trails + Roll/Land และ Inventory 8+2 วันที่ 24 ตุลาคม 2026; กระสุนเส้นแสงเล็ก ยิงอัตโนมัติเร็วขึ้น พร้อม Spiky Hit / Distortion / เสียง วันที่ 25 ตุลาคม 2026 เอกสารนี้สรุปพฤติกรรมที่โค้ดทำอยู่จริง การเชื่อมต่อสำคัญใน Unity Inspector และจุดที่ควรตรวจสอบก่อนพัฒนาต่อ

> หมายเหตุ: working tree ปัจจุบันมีทั้งไฟล์ที่แก้แล้วและไฟล์ใหม่ที่ยังไม่ commit โดยเฉพาะ `WorldPrototype`, Shadow Dash, Energy และ Interaction ดังนั้นให้ถือสถานะไฟล์ปัจจุบันเป็น source of truth และอย่าย้อนทับด้วยเวอร์ชันจาก `main` โดยไม่ตรวจ diff ก่อน

## 1. ภาพรวมโปรเจกต์

- Engine: Unity `6000.5.10f1`
- Render pipeline: Universal Render Pipeline `17.5.0`
- Input: Unity Input System `1.20.0`
- Navigation: AI Navigation `2.0.15`
- UI: uGUI + TextMeshPro
- รูปแบบเกมปัจจุบัน: top-down action shooter ที่เลือกเป้าหมายและเลือกโจมตีระยะไกล/ประชิดให้อัตโนมัติ
- โค้ดเกมหลักอยู่ที่ `Assets/_Project/Scripts`
- Input asset ที่ Player ใช้จริงคือ `Assets/_Project/Input/PlayerControls.inputactions`

## 2. ฉากที่ต้องรู้

### `Assets/_Project/Scenes/WorldPrototype.unity`

นี่คือฉากที่มีระบบล่าสุดครบที่สุด ได้แก่ Player รุ่นใหม่, Energy UI, Shadow Dash, Interaction, Enemy Wave, HUD, Game Over, Camera Rig และ NavMesh ของโลกใหม่ จึงควรใช้ฉากนี้เป็นฉากหลักสำหรับพัฒนาต่อในตอนนี้

สถานะสำคัญ:

- ฉากและโฟลเดอร์ NavMesh ยังเป็นไฟล์ untracked
- Player เริ่มที่ตำแหน่งประมาณ `(-60, 0.1, -60)`
- มี 6 spawn points และ Enemy Wave 4 ชุด (ชุดสุดท้าย Spider ×3 + Bear ×1)
- มี `TestInteractable` สำหรับทดสอบปุ่ม Interact
- มี `TrainingClubPickup` บน `TrainingClubStand` ใกล้จุดเริ่ม Player ที่ตำแหน่ง `(-58, 1, -58)` สำหรับทดสอบเก็บอาวุธประชิดด้วย E
- มี `World/Environment` และโซน Abandoned Village, Forest, Summoning Ruins
- Player ใช้ `HumanMalePlayerVisual.prefab` เป็นร่างและมือปัจจุบันพร้อม Rifle เดิมของ Space Soldier; โมเดลที่วางเปรียบเทียบแยกบน root ของฉากไม่ใช่ตัวที่รับ input
- มีหน้า Inventory 8 ช่องของทั่วไป + 2 ช่องอาวุธประชิด (I/Tab) พร้อม pause/input gate เชื่อมกับ HUD เดิมแล้ว

### `Assets/_Project/Scenes/PrototypeArena.unity`

เป็นฉาก prototype รุ่นก่อน มีระบบ combat และ wave เดิม และเชื่อมหน้า Inventory/Hotbar/ระบบพักเกมแล้ว แต่ระบบอื่นและโลกยังไม่ครบเหมือน `WorldPrototype`

NavMesh asset เดิมของฉากนี้ถูกลบออกจาก working tree แล้ว ถ้าจะกลับมาใช้ฉากนี้ต้อง bake NavMesh ใหม่หรือกู้ asset เดิมอย่างตั้งใจ

### Build Settings ที่ยังไม่ตรงกับงานล่าสุด

Build Settings ปัจจุบันมีเพียง:

1. `Assets/Scenes/SampleScene.unity`
2. `Assets/_Project/Scenes/PrototypeArena.unity`

`WorldPrototype.unity` ยังไม่ได้อยู่ใน Build Settings จึงยังไม่รวมใน build จริง ต้องเพิ่มฉากและจัดลำดับก่อนทำ build สำหรับแจกเล่น ส่วน `GameOverUI.RestartGame()` รองรับการ reload ฉากที่ยังไม่มี build index ระหว่างเล่นใน Editor แล้ว โดยใช้ Editor-only fallback และไม่เปลี่ยนลำดับฉากของโปรเจกต์

## 3. Controls ปัจจุบัน

| Action | Keyboard | Gamepad | พฤติกรรม |
| --- | --- | --- | --- |
| Move | WASD | Left Stick | เคลื่อนที่บนแกน XZ |
| Run | Left Shift | ยังไม่มี | เพิ่มความเร็วขณะมีทิศทางเดิน |
| Dash | Space | Button South | short dash ตาม input หรือทิศที่หันอยู่ |
| Attack | J | Right Trigger | กดค้างยิงต่อเนื่องทุก 0.18 วินาที; ประชิดใช้จังหวะคลิปเดิม |
| Interact | E | Button South | interact กับวัตถุใกล้ที่สุดในระยะ |
| Shadow Dash | M | Left Shoulder | กดค้างเพื่อเคลื่อนที่แบบเงาและกิน Energy |
| Inventory | I หรือ Tab | Select | เปิด/ปิดกระเป๋าและพักเกม single-player |
| Close inventory | Escape | Button East | ปิดกระเป๋า กลับเข้า gameplay |

ข้อควรระวัง: `Dash` และ `Interact` ใช้ Gamepad Button South ปุ่มเดียวกัน ขณะอยู่ใกล้สิ่งที่ interact ได้ การกดครั้งเดียวอาจเกิดทั้ง dash และ interact ควรตัดสินใจว่าจะให้เป็น contextual input จริงหรือแยก binding

## 4. Gameplay flow ปัจจุบัน

```text
PlayerTargeting สแกน Enemy layer
        |
        +-- ศัตรูใกล้กว่า 1.8 m --> Close Override --> Melee
        |
        +-- เป้าหมายอื่นใน 12 m --> Ranged
                         |
PlayerAttackController อ่าน Attack และ cooldown
        |
        +-- MeleeAttackRequested  --> หันหาเป้า --> wind-up --> เช็กระยะอีกครั้ง --> damage
        |
        +-- RangedAttackRequested --> รอหัน/ท่าถือปืน/IK พร้อม
                                      --> spawn ตาม MuzzlePoint.forward
                                      --> RangedShotFired + cooldown + animation
                                      --> damage เมื่อชน
```

ไม่มี manual aim ในตอนนี้ ผู้เล่นโจมตี `CurrentTarget` ที่ระบบเลือกให้เท่านั้น

## 5. Player systems

### Movement — `PlayerMovement.cs`

ค่าที่ wire อยู่ใน `WorldPrototype`:

- Walk/ขยับสั้น ๆ ช่วงความเร็วถึง `2 m/s`
- Run ปกติ `5 m/s` (กด WASD ค้าง)
- Sprint `10 m/s` (Shift + WASD)
- acceleration `18 m/s²`, deceleration `26 m/s²` ทำให้ tap สั้นผ่าน Walk ก่อนขึ้น Run และปล่อยแล้วผ่อนจนหยุด ไม่เปลี่ยนความเร็วทันทีทั้งเวกเตอร์
- หมุน `15`
- short dash ระยะ `5 m`, เวลา `0.18 s`, cooldown `0.65 s`
- shadow dash `18 m/s`

ลำดับความสำคัญของ state คือ Short Dash > Shadow Dash > Walk/Run/Sprint; interaction lock หยุดและล้าง momentum ทันที

`PlayerMovement` คง serialized field `moveSpeed` (Inspector ชื่อ Run Speed) และ `runSpeed` (Inspector ชื่อ Sprint Speed) เพื่อไม่ทำค่าฉากเดิมสูญหาย แต่ public API ปัจจุบันคือ `WalkSpeed` = เดิน, `RunSpeed` = วิ่งปกติ, `SprintSpeed` = วิ่งเร็ว `IsRunning` ยังหมายถึงกดปุ่ม Run/Shift เพื่อคงพฤติกรรม camera bob เดิม และมี alias `IsSprinting` การหมุนใช้ exponential interpolation ที่สอดคล้องกันตาม deltaTime อัปเดตความเร็วที่เก็บไว้จาก CharacterController เมื่อมี `CollisionFlags.Sides` ป้องกันสะสม momentum ติดผนัง และตั้ง `minMoveDistance = 0` ใน Awake เพื่อไม่ทิ้ง displacement เล็กช่วงออกตัวที่เฟรมเรตสูงจนเร่งไม่ขึ้น

Short Dash:

- กดครั้งเดียวและใช้ทิศ input ปัจจุบัน
- ถ้าไม่มี input จะพุ่งไปตาม `transform.forward`
- หยุด Shadow Dash ก่อนเริ่ม
- มี trail, character stretch, dash animation และ camera FOV punch
- ไม่ได้ทำให้เป็นอมตะ

Shadow Dash:

- ต้องมี Energy อย่างน้อย `10` จึงเริ่มได้
- กิน Energy `30/วินาที`
- เมื่อ Energy หมด ต้องปล่อยปุ่มหนึ่งครั้งก่อนเริ่มใหม่ (`shadowNeedsRelease`)
- ระหว่างใช้งาน เล่นช่วงโดดต้น Roll แล้วแปลงเป็น Ice Trails/หัวพลังทรงมน; ปล่อยแล้ว NinjaJump_Land ผสมกลับ locomotion การปิดโจมตีและอมตะเริ่มทันทีตาม gameplay ไม่รอภาพแปลงร่าง
- root GameObject เปลี่ยนจาก layer ปกติเป็น `PlayerShadow`
- Physics matrix ปัจจุบันตั้งให้ `PlayerShadow` ไม่ชน `Enemy` และไม่ชน `PlayerShadow` ด้วยกัน แต่ยังชน `Environment`

### Shadow Dash ปัจจุบัน — Ice Trails + Roll → NinjaJump_Land (24 ตุลาคม 2026)

- ใช้หางจาก asset จริง `Assets/Vefects/Trails VFX/VFX/Particles/VFX_Trail_Ice.prefab` สำเนาแบบ unpack ไว้ใต้ prefab โปรเจกต์เดิม `Player/ShadowDashTrails/Ice Trail Asset` (glow/secondary/distortion TrailRenderer 3 ชั้น) เก็บ noise/mask/erosion textures ต้นฉบับ ใช้ material สำเนา URP ใน `Assets/_Project/Materials/ShadowDash/M_IceTrail_*.mat` ไม่แก้ shader/material/prefab ของ Trails ต้นฉบับเพื่อเปลี่ยนลุค
- เพิ่มเฉพาะ **หัวพลัง 3D ปลายมน** ผ่าน `ShadowDashIceForm.cs` และ `IceTrailHeadURP.shader`: หน้ากลม ด้านหลังเรียวไหลเข้าหาง พร้อม noise/การพลิ้วเบา ๆ แก้หน้าที่ตัดแบนของแถบ Trail; ไม่มี collider ไม่เพิ่ม hitbox ไม่ใช่เกลียว mesh แบบเวอร์ชันก่อน และไม่เปลี่ยน Trail ของ Short Dash
- กด M: physics/energy/invulnerability เริ่มทันที; ภาพเล่น **UAL1 Roll เฉพาะช่วงต้น 0.26 s** (state ShadowRollEnter speed 1.35) ไม่รอกลิ้งครบ แถบลมเริ่ม emit ตอน 70% ของช่วงนี้ ตัวละคร/อาวุธยุบเข้าหัวพลังอย่างนุ่มนวลแล้วซ่อนเมื่อ scale ถึงศูนย์ ช่วงนี้ไม่ใช้ root motion
- ปล่อย M/พลังงานหมด: หยุด emission แล้วปล่อยหางจางตาม lifetime; เริ่ม **UAL2 NinjaJump_Land** (state ShadowNinjaLand) ก่อนตัวละครขยายกลับและหัวพลังจางใน 0.22 s ท่าลงพื้นเล่นความยาวประมาณ 0.48 s; เมื่อมีการเดินอยู่จะผสมกลับ locomotion ที่ normalized time 0.48 ด้วย blend 0.12 s ถ้ายืนเฉย ๆ เก็บ recovery ยาวถึง 0.82 ไม่มีการล็อกการเคลื่อนที่เพิ่ม
- Dash ปกติ Space ใช้ **UAL2 Shield_Dash** แทน Roll (state เปลี่ยนชื่อ DashRoll → DashShield) เล่นจบประมาณ 0.23 s แล้ว blend กลับ; ระยะ/ความเร็ว/cooldown/camera/trail เดิมไม่ถูกเปลี่ยน กติกาเดิม Space ไม่แทรกขณะกด M ค้างยังอยู่ แต่ Space หลังปล่อย M ยกเลิกภาพคืนร่างและคืนขนาดตัวก่อนเริ่ม Shield Dash ได้
- คลิป humanoid สำเนาอยู่ `Assets/_Project/Animations/Player/Dash/`: `ShadowRoll_Enter.anim`, `ShadowNinjaJump_Land.anim`, `Shield_Dash.anim`; bake root transforms ในสำเนา ไม่แก้ FBX/การ import ของ UAL1/UAL2, ไม่เปิด root motion บน Player, ไม่ล้าง locomotion blend trees/weapon layers/appearance overrides เดิม
- ปรับ **Shadow Dash Trail Vfx**: สี material 1/2, gradient, intensity, Ice Tail Brightness, width/lifetime/secondary multipliers, texture speed, opacity, distortion และเสียง ปุ่ม Ice คืนโทนต้นแบบ, Fire/Purple/Azure/Crimson เป็นตัวอย่าง recolor ของ Ice recipe (ไม่ใช่สลับไป prefab ธาตุอื่นทั้งชุด)
- ปรับ **Shadow Dash Ice Form**: Entry Duration, Shrink Start Fraction, Return Duration, Radius, Head Length, Forward Offset, Turn Responsiveness และ Flutter; หัวหมุนตาม velocity แนวนอนจริง หางมี round caps และค่อยเรียวปลาย ทั้งหัวและหางเปลี่ยนสีร่วมกันผ่าน MaterialPropertyBlock แยก instance ไม่เขียน shared material ทุกเฟรม
- เสียงลม Ice จากแพ็กกลับมาแล้ว `Vefects_SFX_Trail_Ice.wav` เล่นเมื่อ Trail เริ่ม emit/fade ตอนปล่อย เปิด/ปิด Volume/Pitch/Fade Out ได้; ถ้าไม่มี Opaque Texture จะปิดเฉพาะ distortion ส่วนหัว/หางยังทำงาน
- `PlayerDashVisual.Update` ส่งสถานะก่อน weapon/animation Update แล้ว LateUpdate ค่อย commit scale/visibility; `PlayerWeaponVisual` คงอาวุธไว้ช่วง Roll ก่อนซ่อน; Animator ลด weapon layers ระหว่าง Roll/Trail/Land เพื่อไม่ให้ท่าถือปืนทับท่าลงพื้น ระบบยิงยังรอ ranged pose จริงตามเดิม
- ปุ่มสั้น/กดซ้ำต่อจาก scale ปัจจุบัน ไม่ snap; disable visuals จะคืน scale/renderer และหยุดเสียงทันที; teleport clear ประวัติหาง; energy exhaustion คืนร่างแล้วต้องปล่อย M ก่อนเริ่มใหม่ โค้ดเกลียว `ShadowDashWindVfx` เก็บไว้แต่ disabled ไม่วาดทับ Ice
- **Tools → VFX → Install Ice Trails and Dash Animations** หรือเมนู Build and Install Shadow Trails เดิม ติดตั้งใน prefab/controller ที่ทั้งสองฉากใช้ร่วมกัน ไม่เขียนทับฉากที่ผู้ใช้เปิดค้างและสำรองก่อนแก้ใน `Logs/PrefabBackups/*BeforeIce*` กับ `Logs/AnimationBackups/*BeforeIce*`; rerun ไม่รีเซ็ตรูปร่าง/สีที่จูนหลังติดตั้ง
- รายงานคลิปจริง `Logs/IceDashClipInspection.txt` (Roll 1.4667 s, Land 1.2667 s, Shield 1.1 s ที่ 30 fps ก่อนกำหนดความเร็ว state) ทดสอบ **Tools → VFX → Run Shadow Trail Regression** มี branch Ice ครอบคลุม state/คลิปจริง, asset textures, หัวมน, entry/reveal/landing, M/Space, พลังงาน, เสียง, rapid tap, teleport, instance customization และ disable cleanup ภาพ `Logs/IceDash_Roll.png`, `IceDash_Active.png`, `IceDash_GameView.png`, `IceDash_Land.png`
- แพ็ก Demo ที่ reimport กลับมามี OUTPUT_SH4 แบบเก่าที่ไม่ตรง URP 17.5 จึงแพตช์กลับเป็น OUTPUT_SH 2 จุดเช่นเดิม เฉพาะ shader ตัวละคร Demo ไม่ใช่ shader/material ของ Trails สำรองรอบนี้ `Logs/ShaderBackups/SH_Vefects_Demo_BeforeIceReimport.shader.txt`

ผลตรวจ Ice เวอร์ชันล่าสุด 24 ตุลาคม 2026: `ShadowDashVfxRegression` **ALL CHECKS PASSED** รวมคลิป Roll/Shield/Land จริง, ไม่มี root motion, หัวมน/asset textures, สีแยก instance/คืนสีต้นแบบ, การลงพื้นไม่ถูก melee/pickup ทับ, rapid input, พลังงานและ cleanup; `CombatAimingRegression` **ALL CHECKS PASSED** (38 กระสุน/38 confirmed/47 requests); C# build **0 warnings / 0 errors** ภาพทั้ง 4 ช่วงตรวจจาก Play Mode แล้ว ไม่บันทึก fixtures ลงฉาก

### เวอร์ชันก่อน — เกลียวลมแนวนอน Cartoon (ปิดใช้งานแล้ว)

- เอฟเฟกต์เวอร์ชันก่อนคือ `ShadowDashWindVfx.cs`: mesh แถบลมปลายเรียวหมุนเกลียว **รอบแกนแนวนอนตามทิศเคลื่อนที่จริง** ไม่ใช่พายุแกนตั้ง ไม่ใช่เส้น TrailRenderer ลากประวัติการเดิน แถบหลัก 3 เส้นกับลมเส้นเล็กอีก 3 เส้นมีคลื่นสะบัดตามเวลาและหมุนต่อเนื่อง มุมหัน interpolate ด้วย exponential smoothing
- เริ่มกด M: การเคลื่อนที่/พลังงาน/อมตะทำงานทันที แต่ภาพค่อย ๆ เพิ่มลมคลุมตัวประมาณ `0.38 s` ก่อนซ่อน renderer ของ Player; ปล่อย M/พลังงานหมด: ให้ลมคลุมก่อนเปิด renderer กลับ แล้วคลายลมประมาณ `0.36 s` ไม่มีการเพิ่ม input delay หรือแก้เงื่อนไขการโจมตี/ชนเดิม
- การสลับ body เกิดใต้ shell ลมที่ปิดช่องครบ `CoverAmount = 1`; ใช้สถานะภาพแยกจาก `IsShadowDashing` และประเมินเวลาเดียวกันทั้ง PlayerDashVisual กับ VFX หากเฟรมช้าข้ามช่วงคลุม จะรักษา shell เต็มไว้อย่างน้อยหนึ่งเฟรม ไม่เปลี่ยน material ของผิว/ผม/ปืนเพื่อ dissolve และไม่ทำลายระบบปรับแต่งตัวละคร
- กดแล้วปล่อยสั้นมากจะไม่บังคับซ่อนตัว; กดซ้ำกลางการคืนร่างเริ่มต่อจากขนาด/การคลุมปัจจุบัน ปิด PlayerDashVisual จะคืน body และปิด mesh ทันที; ปิด Movement จะจบ transition ตามปกติ ไม่มี body/ลมค้าง; mesh เป็น local volume จึงไม่ลากข้ามฉากเมื่อ teleport
- ทั้ง WorldPrototype และ PrototypeArena เชื่อม `PlayerDashVisual.shadowDashVfx` กับ `Player/ShadowDashTrails` และใช้ prefab `Assets/_Project/Prefabs/VFX/ShadowDashTrails.prefab` เดียวกัน ต้องอยู่ **นอก Player/Visual** เพื่อไม่ถูกซ่อนไปพร้อมตัวละคร
- แพ็ก `Assets/Vefects/Trails VFX` ถูกเอาออกจาก workspace ระหว่างเปลี่ยนเอฟเฟกต์ ทำให้ prefab variant เดิมกลายเป็น Missing Source จึงแปลง prefab โปรเจกต์เป็น **standalone** โดยคง GUID และ fileID ของ root/Transform/controller เพื่อรักษาลิงก์ทั้งสองฉาก ไม่ดึงแพ็กที่ถูกลบกลับและไม่เขียนทับฉากที่ผู้ใช้กำลังแก้ สำรอง variant ที่เสียก่อนแก้ไว้ `Logs/PrefabBackups/ShadowDashTrails_MissingSource_BeforeStandalone.prefab`
- เลือก **Player → ShadowDashTrails → Shadow Dash Wind Vfx** ปรับ Length, Radius, Ribbon Width/Count, Turns, Rotations Per Second, Flutter, Direction Responsiveness, White Mix, Enter/Exit Duration และ Cover Half Height/Radius/Half Length ได้ ค่าตั้งต้น length `3.3 m`, radius `0.95 m`, width `0.72 m`, turns `0.85`, หมุน `1.6 รอบ/วินาที`, flutter `0.13 m`, White Mix `0.96`; cover เผื่อพื้นที่มือ ปืน และอาวุธที่หลังไว้แล้ว PlayerWeaponVisual ใช้จังหวะซ่อนภาพเดียวกัน ไม่ปิดปืน/อาวุธทันทีที่กด M
- **Shadow Dash Trail Vfx** ตัวเดิมยังจัดการสี/preset, Intensity, Texture Animation Speed (คูณการหมุน/พลิ้ว) และเสียง ใช้ปุ่ม Purple Shadow / Azure Void / Crimson Dark ได้ สีหลักยกเป็นขาวนวล ขอบเป็นสีอ่อนตาม preset ไม่ใช่ควันดำ/เส้นแข็ง ค่า Width/Lifetime/Opacity/Distortion ในคอมโพเนนต์เดิมเป็นของโหมด legacy เท่านั้น
- Shader `Assets/_Project/Shaders/CartoonShadowWind.shader` เป็น URP unlit สองสี, double-sided, opaque cutout และเขียน depth ไม่ต้อง VFX Graph/Amplify/Opaque Texture/GrabPass; ไม่มี noise texture ภายนอก วัสดุ `Assets/_Project/Materials/ShadowDash/M_CartoonShadowWind.mat` ถูกอ้างอิงจาก prefab จึงรวมใน build; mesh สร้างครั้งแรกเมื่อใช้งานและนำ buffers กลับมาใช้ สีใช้ MaterialPropertyBlock แยกแต่ละ instance ไม่แก้ shared material
- การปรับระหว่าง Play ใช้ preview เท่านั้น ต้องแก้ prefab ใน Edit Mode หรือ Apply override อย่างตั้งใจหากต้องการบันทึก ใช้ component API `SetDashing(bool)`, `SetPalette(...)`, `StopImmediately()`, `ShouldHideCharacter`, `CoverAmount`, `Envelope`, `Axis` ได้ โดยปกติให้ PlayerDashVisual/ShadowDashTrailVfx เป็นผู้สั่ง ไม่เรียกสลับหลายที่
- เสียงเป็น optional: AudioSource อยู่บน prefab และเปิด Loop/ปิด Play On Awake แล้ว แต่ **ยังไม่มี clip** เพราะไฟล์เสียงแพ็กเดิมถูกเอาออกด้วย สามารถ assign เสียงลมใหม่ได้ที่ AudioSource; controller รองรับ Volume/Pitch/Fade Out อยู่แล้ว ไม่สังเคราะห์เสียงหรือโหลดเสียงภายนอกเอง
- Legacy ShadowDashParticles ยัง inactive ใน WorldPrototype; โค้ด trail/URP shaders เก่ายังเก็บไว้เพื่ออ้างอิง แต่ standalone wind prefab ไม่มี TrailRenderers และไม่ต้องพึ่ง textures ที่หาย ถ้าจะกลับไป legacy ต้องมี renderer/material/textures ครบก่อน ไม่ใช่เพียงปิด Use Wind Vortex
- เมนู **Tools → VFX → Upgrade to Flowing Cartoon Wind** เพิ่ม/เชื่อม component กับ material ใน prefab โดยไม่รีเซ็ตค่ารูปร่าง/สีที่จูนไว้และสำรอง prefab ก่อน; **Run Shadow Trail Regression** ตรวจโหมดลมปัจจุบันด้วย input M/Space จริงใน Play Mode รวม cover ก่อน body สลับ, แกนแนวนอน/เปลี่ยนทิศ/พลิ้ว, instance isolation, ไม่มี Opaque Texture, teleport, rapid re-entry, energy exhaustion และ disable cleanup รายงาน `Logs/ShadowDashVfxRegression.txt`, ภาพ `Logs/ShadowWind_*.png`

ผลตรวจของเวอร์ชัน Wind ก่อนเปลี่ยนเป็น Ice วันที่ 24 ตุลาคม 2026: Wind regression `ALL CHECKS PASSED` (รวมอาวุธที่หลังและจำลองเฟรมข้าม transition), Combat aiming regression `ALL CHECKS PASSED` (37 กระสุน/37 confirmed shots), C# build 0 warnings / 0 errors ชุดทดสอบ aiming รอการหันกลับตาม `turnSpeed` ของฉากแทน timeout คงที่ ไม่ได้เปลี่ยน gameplay หรือค่าความเร็วที่ผู้ใช้จูนไว้ในฉาก (WorldPrototype Shadow Dash 18 m/s, Arena 11 m/s)

### Energy

- สูงสุด `100`
- ฟื้น `20/วินาที`
- เริ่มฟื้นหลังหยุดใช้ `0.75 s`
- `PlayerEnergy.EnergyChanged` เป็น event หลักสำหรับ UI

### Targeting — `PlayerTargeting.cs`

- สแกนด้วย `Physics.OverlapSphereNonAlloc` ทุก `0.15 s`
- ใช้เฉพาะ layer `Enemy` (layer 6)
- ระยะเป้าหมายทั่วไป `12 m`
- ค่า priority ในฉากคือ `Nearest`; โค้ดรองรับ `LowestHealth` ด้วย
- ถ้าศัตรูเข้าระยะ `1.8 m` จะ override เป็นเป้าประชิด และล็อกไว้จนออกเกิน `2.2 m` เพื่อลดการสลับโหมดถี่
- เป้าระยะไกลเดิมจะถูกล็อกต่อจนตายหรือออกนอกระยะ ไม่ได้เลือกตัวที่ใกล้กว่าใหม่ทุก scan
- ไม่มี line-of-sight check ระบบอาจเลือกศัตรูหลังกำแพง แต่ projectile จะหายเมื่อชน collider ที่ไม่ใช่ trigger
- buffer รองรับ collider สูงสุด 64 รายการต่อ scan; ถ้าเกินจะถูกตัดเงียบ ๆ

### Combat

- `PlayerAttackController` เป็นตัวตัดสินโหมดและ broadcast event ไม่ทำ damage เอง
- ระหว่าง Short Dash หรือ Shadow Dash จะตั้งโหมดเป็น `None` และโจมตีไม่ได้
- Ranged: damage `20`, projectile speed `18`, projectile lifetime `3 s`
- Melee: damage `15`, hit range `2.2 m`, สลับคลิป `Armature|Punch_Jab` และ `Armature|Punch_Cross` ทุกครั้งที่เริ่มหมัด โดยทั้งคู่ใช้ speed `2`
- เมื่อมีอาวุธประชิดใน `PlayerMeleeEquipment` จะใช้ `Sword_Attack` ที่ state `Base Layer.MeleeWeaponAttack` แทนหมัด โดยอ่าน damage, kill mode, range และ hit time จาก `MeleeWeaponDefinition` แล้วเก็บค่าสำหรับการเหวี่ยงครั้งนั้น
- กระบองต้นแบบ `TrainingClub.asset`: เปิด `Kills In One Hit` จึงตีโดนหนึ่งครั้งแล้วศัตรูตาย ไม่ขึ้นกับจำนวน HP; hit range `2.5 m`, hit normalized time `0.35`, ท่าอาวุธ speed `1.75` ค่า damage `30` จะใช้เฉพาะเมื่อปิด kill mode
- One-hit ยังเช็กระยะและจังหวะคลิปตามเดิม แล้วเรียก `EnemyHealth.TakeDamage` ด้วย HP ที่เหลือ ณ ตอนโดน จึงยังส่ง event การตาย/นับ wave ตามปกติ ไม่เปลี่ยน damage ของหมัดหรือปืน
- จังหวะ damage อ่านจาก normalized time ของ Animator โดยค่า `hitNormalizedTime = 0.35` หมายถึง 35% ของคลิป ปรับค่านี้ใน `PlayerMeleeAttack` หลัง preview ท่าได้โดยไม่ต้องแก้ FBX
- เช็กระยะซ้ำตอนหมัดโดนและให้ damage ได้ครั้งเดียวต่อหมัด ถ้าเป้าหมายออกนอกระยะหรือตายจะไม่โดน
- ระหว่างท่าต่อยเดิมยังไม่ออกจาก Animator จะไม่เริ่มยิงหรือต่อยซ้อน; Short Dash, Shadow Dash, การตายหรือปิด component จะยกเลิกหมัดที่ค้าง
- ถ้าไม่มี Animator/state ที่ใช้งานได้ จะไม่ทำ damage แบบไม่มีท่า และปล่อยสถานะโจมตีหากท่าไม่เริ่มภายใน `animationStartTimeout = 0.5 s`
- ปืนใช้ `rangedAttackInterval = 0.18 s` (สูงสุดประมาณ 5.56 นัด/วินาทีเมื่อเล็งพร้อม) แยกจาก `attackInterval = 0.35 s` ของประชิดซึ่งยังรอคลิปเดิมจบ สำหรับปืนเริ่ม cooldown เมื่อกระสุนออกจริง ไม่ใช่ตอนขอยิง และไม่สะสมหลายคำขอยิงระหว่างรอเล็ง
- ขณะกดโจมตีระยะไกล `PlayerCombatFacing` ให้ทิศเล็งกับ `PlayerMovement` ซึ่งเลือกการหันเพียงครั้งเดียวก่อนเล่น Animator: หันหา enemy อย่างต่อเนื่องด้วย `rotationSpeed = 1080°/s` ไม่ snap ตอนยิง และเดินข้าง/ถอย/เฉียงได้โดย root ไม่หันตามทิศเดินแทรก ปล่อยปุ่มแล้วค้างเล็งได้อีก `0.5 s` ก่อนกลับไปหันตามการเดิน
- `PlayerMeleeAttack` ควบคุมการหันเองตลอดช่วงต่อยและเช็กระยะซ้ำตอน hit frame

#### การเล็งและปล่อยกระสุน (24 ตุลาคม 2026)

- `PlayerRangedWeapon` เก็บ pending target ได้เพียงหนึ่งตัว; ปล่อย Attack, เป้าตาย/ปิด/เปลี่ยน/ออกนอกระยะ, เข้า close override, Dash, Shadow Dash, interaction lock, Player ตาย หรือปิด attack component จะไม่ปล่อยนัดค้างไปยังเป้าเก่า การเปลี่ยนเป้าขณะกดค้างต้องเล็งเป้าใหม่ก่อน
- ยิงได้เมื่อ `IsRangedPoseReady`: อยู่ Locomotion ไม่ติด transition ไปท่าทั้งตัว, gun layer น้ำหนักอย่างน้อย `0.99` และ state ปืนอยู่ GunIdle/GunShoot ไม่ใช่ Empty; root ห่างทิศเป้าไม่เกิน `maxBodyAimError = 5°` และ **แนวปลายปืนจริง** ห่างเป้าไม่เกิน `maxMuzzleAimError = 2°`
- `PlayerRifleHandIK` ปรับแนวมือขวา/ปืนเข้าจุดเล็งอย่างนุ่มนวลหลังแก้ตำแหน่งแขน แล้วแก้มือซ้ายตามจุดรองปืน จำกัดทิศเล็งรอบ root ที่ yaw ±35° / pitch ±30° และ `aimRotationSpeed = 720°/s` จึงไม่หมุนมือยิงย้อนหลังระหว่าง root ยังหันไม่ทัน ถ้าเป้าสูง/ต่ำเกินขอบเขตนี้จะไม่ฝืนปล่อยกระสุนที่ลำกล้องยังไม่ตรง
- ลำดับสำคัญ: Update `PlayerTargeting(-200)` → `PlayerMovement(-100)` → `PlayerAttackController(0)` → `PlayerWeaponVisual(50)` → `PlayerAnimationController(100)` → Animator evaluation → LateUpdate `PlayerLocomotionSpineProxy(150)` → `PlayerRifleHandIK(175)` → `PlayerRangedWeapon(200)` อย่าย้ายการ spawn กลับไปอยู่ใน callback ขอยิงก่อน Animator/IK
- กระสุนเกิดที่ตำแหน่งปลายปืน ณ หลังแก้ pose แล้ว และใช้ `MuzzlePoint.forward` เป็น velocity ตอนออก ไม่คำนวณทิศแยกตรงไปหา enemy และไม่ homing หลังปล่อยแล้ว `Projectile` ยังใช้ damage 20 / speed 18 / lifetime 3 s ตามเดิม
- Event `RangedAttackRequested` หมายถึง intent เท่านั้น; animation เปลี่ยนไปฟัง `RangedShotFired` ซึ่งยืนยันว่ากระสุนออกแล้ว ส่วน `PlayerRangedWeapon.ProjectileFired(Projectile)` ใช้ต่อ VFX/เสียง/ตรวจแนวกระสุนจริง ห้ามสร้างกระสุนซ้ำจากทั้งสอง event
- ชุดทดสอบ opt-in อยู่ `Assets/_Project/Tests` เปิดฉาก WorldPrototype หรือ PrototypeArena แล้วเลือก **Tools → Combat → Run Aiming Regression** จะใช้ PlayerInput/Animator/IK/Projectile จริงใน Play Mode พร้อมพื้น/ศัตรู/Keyboard ชั่วคราว ทดสอบถอยหลัง, ข้าง, เฉียง, Sprint, 30/60/120 FPS, กลับตัวจากหลัง, เปลี่ยน enemy, กระสุนไม่ homing, คูลดาวน์, ยกเลิกนัดค้าง และกลับจากหมัดมาปืน รายงาน local `Logs/CombatAimingRegression.txt` หลังจบออก Play Mode และนำ input ทดสอบออก ไม่แก้ฉากหรือ input binding ที่บันทึกไว้
- ผลรันล่าสุดบน WorldPrototype วันที่ 25 ตุลาคม 2026 หลังปรับ cadence: `ALL CHECKS PASSED` ยิงจริง 64 นัดตรงกับ confirmed event 64 ครั้งจาก 74 requests (รวมการยกเลิก) ผ่านเป้าเคลื่อนที่ขณะ strafe, ปล่อยยิงแล้วคืนทิศเดิน, ปิด movement/attack และศัตรูออกนอกระยะระหว่าง wind-up; C# build 0 warnings / 0 errors ชุดทดสอบถูกหุ้ม `UNITY_EDITOR` ไม่รวมใน player build และสำรองรายการ input devices เป็นสำเนา ไม่เก็บ view ของ array ที่ Input System เปลี่ยนค่าภายหลัง

### กระสุนเส้นแสง + Spiky Hit / Distortion / เสียง (25 ตุลาคม 2026)

- Player ทั้ง WorldPrototype และ PrototypeArena ยิง `Assets/_Project/Prefabs/Projectiles/PlayerEnergyTracer.prefab` แทนลูกบอลเดิม; `Projectile.prefab` ต้นฉบับยังอยู่ ไม่ถูกเขียนทับ
- ภาพเป็นเส้นแกนแสงสีทองอุ่นยาว 0.58 m กว้าง 0.028 m พร้อม glow กว้าง 0.07 m และ tail สั้น 0.045 s; ปิด mesh ลูกบอลเดิม การแสดงภาพไม่เปลี่ยน SphereCollider รัศมีโลก 0.1 m, speed 18, damage 20 หรือ lifetime 3 s
- เส้นใช้ LineRenderer local-space จึงชี้ตาม pose ตอนออกจากปลายปืนตลอดนัด ไม่มี homing/เลี้ยวตาม enemy; Ranged aim readiness/IK และ cancellation เดิมยังเป็นผู้อนุญาตการยิง
- `PlayerRangedFeedback` ฟัง **ProjectileFired** เท่านั้น เล่น muzzle flash กับเสียงเมื่อสร้างกระสุนจริง ไม่เล่นจาก RangedAttackRequested ที่อาจถูกยกเลิก; muzzle รักษาขนาดโลกก่อน parent เข้าปลายปืน เพื่อไม่ขยาย/หดตาม scale ของ rig ที่ import มา
- แสงปลายปืนและตอนชนเป็นสำเนาของ `Lite_VFX_Radial_Spiky_Hit_01_Random_Rotate_Loop` จาก **Flipbook VFX Bundle Lite URP**; ต้นฉบับแพ็กไม่ถูกแก้ สำเนา `RifleSpikyMuzzle.prefab` และ `RifleSpikyImpact.prefab` เปลี่ยนเป็น burst 1 อนุภาค ไม่ loop และไม่มี collider/แรงผลัก/damage
- Muzzle ขนาดอนุภาค 0.32 m อายุ 0.085 s; impact 0.85 m อายุ 0.18 s; prefab จัดการ cleanup แบบ scaled time 0.24/0.7 s ผ่าน `RangedBurstVfx` ดังนั้นเปิด Inventory แล้วหยุดเวลาได้ รวมถึงอาวุธถูกซ่อนระหว่าง dash ก็ไม่ทิ้งวัตถุเอฟเฟกต์ค้างถาวร
- Material ของ Spiky คัดลอกเฉพาะ Alpha/Mask texture และสีที่ Lite shader ใช้จริง ไม่ดึงช่อง texture เก่าที่อ้างอิงหายและไม่ได้ใช้ใน shader มาเป็น dependency
- `Projectile.impactVfx` spawn เมื่อกระทบเป้าหมายที่มีชีวิตหรือ collider ไม่ใช่ trigger เท่านั้น; เช็ก owner และ `hasHit` เดิม จึงไม่ยิงโดนตัวเอง ไม่เกิดหลาย damage จากหลาย collider และไม่เกิด impact เพียงเพราะหมดอายุกลางอากาศ
- เสียงยิงใช้ `Assets/_Project/Audio/Gun/pistol-sound-effect.mp3` ตามที่ผู้ใช้เลือกวันที่ 25 ตุลาคม 2026 (ไฟล์ต้นฉบับประมาณ 1.896 s ไม่ตัด/เขียนทับ), volume 0.2 และสุ่ม pitch 1.02–1.12 เท่าเดิม; ปรับที่ Player > PlayerRangedFeedback และเสียงกระทบยังใช้ `SFX_Vefects_Projectile_Burst_Impact_03.wav` volume 0.1 บน impact prefab ไม่มี sound loop/autoplay ของเสียงยิง และไม่ ignore AudioListener.pause
- Distortion ทำเองด้วย `RangedRefractionURP.shader` + `RangedRefraction`: quad ใสหันเข้ากล้องใช้ procedural ring/noise ไม่ต้องซื้อแพ็ก/Texture เพิ่ม บิด **ภาพฉาก opaque ด้านหลัง** แยกจากแสง Spiky ไม่ใช่แค่บิด UV ของแสง ไม่มี refraction ของ HUD หรือวัตถุโปร่งใสที่ render ภายหลัง
- มี refraction อ่อน ๆ รอบ bullet (radius 0.16, strength 0.0012), รอบ muzzle ขยาย 0.045→0.34 m ใน 0.13 s (strength 0.002), และ impact ขยาย 0.1→0.68 m ใน 0.23 s (strength 0.0035) พร้อม fade เข้า/ออก ปรับ radius/duration/strength/opacity ที่ RangedRefraction ของแต่ละ prefab
- ต้องมี Camera Opaque Texture; PC_RPAsset เปิดอยู่แล้ว Mobile_RPAsset ยังปิด หากกล้องปิด opaque texture จะซ่อนเฉพาะ refraction อย่างปลอดภัย เส้นกระสุนและ Spiky ยังเล่นตามปกติ ไม่เปลี่ยน renderer settings ทั้งโปรเจกต์เพื่อบังคับมือถือ
- Shared Material ไม่ถูกแก้ตอน runtime: refraction ใช้ MaterialPropertyBlock แยกต่อ instance; Material และ prefab ปรับแต่งอยู่ใน `_Project/Materials/Ranged` และ `_Project/Prefabs` ไม่แก้ asset pack
- **Tools > Combat > Install Thin Tracer and Spiky VFX** สร้าง assets เฉพาะที่ยังไม่มี และสำรองฉากรวม unsaved edits ไว้ `Logs/SceneBackups/*BeforeRangedVfx*.unity` ก่อนเชื่อม; การรัน installer ซ้ำจะตั้ง rifle interval ในสองฉากกลับ 0.18 s ส่วน prefab/material ที่มีอยู่แล้วคงการปรับแต่งไว้
- **Tools > Combat > Run Tracer and Spiky VFX Regression** ตรวจ shader, thin geometry/collider, cadence, actual barrel direction, hit damage, one-shot cleanup, audio, pause/resume และเปรียบเทียบ pixel ระหว่างเปิด/ปิด refraction; รายงาน `Logs/RangedVfxRegression.txt`, ภาพ `Logs/RifleVfx_*.png` ทั้งหมดเป็น fixture ใน Play Mode ไม่ถูก save ลงฉาก
- ผลล่าสุด `ALL CHECKS PASSED` 30 ข้อ: ได้ 7 นัดใน 1.280 s, การเทียบเปิด/ปิด refraction ต่างกัน 1,126 pixels บนภาพทดสอบฉากหลังเดียวกัน, ไม่มี burst ค้างหลังหยุดยิง/พักเกม และซ่อน/เปิดปืนไม่เล่น flash เก่าซ้ำ; กระสุนโดนครั้งเดียว 20 damage ตามเดิม

### Weapon และ Animation

- ปืนเป็นความสามารถที่ Player เรียกใช้ได้ตั้งแต่เริ่มเกม ไม่ต้องเก็บ และยังใช้ได้หลังเก็บอาวุธประชิด
- `PlayerWeaponVisual` แสดงปืนเมื่อมีเป้าหมายระยะไกล; อาวุธประชิดที่เก็บไว้จะสะพายเฉียงด้านหลังเมื่อไม่มีศัตรูประชิด รวมถึงตอนถือปืน เดิน และวิ่ง
- เมื่อมีเป้าหมายประชิด (เข้า `1.8 m`, ออก `2.2 m` ตาม `PlayerTargeting`) จะย้ายอาวุธจากหลังเข้ามือขวาและเปิดท่าถือ; ถ้ายังไม่มีอาวุธจะใช้หมัด เมื่อศัตรูตาย/ออกห่างจะสะพายกลับ แต่ถ้ากำลังเหวี่ยงอยู่จะรอให้ท่าจบก่อน
- `PlayerMeleeEquipment` เก็บได้สูงสุด 2 ชิ้น ใช้หนึ่งโมเดลต่อชิ้นสลับ parent ระหว่าง RightHand กับ `MeleeBackSocket` ที่สร้างใต้ UpperChest/Chest/Spine; ชิ้นที่เลือกเท่านั้นเข้ามือ ส่วนอีกชิ้นสะพายหลังตลอด เมื่อสะพายทั้งคู่จะพาดไขว้กันด้วย `Second Back Offset` และ `Second Back Euler Angles` ไม่สร้างโมเดลเพิ่มทุกครั้งที่สลับ
- ระหว่างหยิบของหรือ dash จะซ่อนอาวุธทั้งสองแบบ; ท่าฟาดด้ามปืนยังไม่ได้ทำ
- เมื่อไม่มีเป้าหมาย ปิด gun visual, ตั้ง `HasGun = false` และ blend weight ของ upper-body layer ไปที่ 0
- ระหว่าง dash layer ปืนถูก blend ออก
- เริ่มต่อยจะปิด weight ของ `WeaponUpperBody` ทันที เพื่อไม่ให้ท่าถือปืนทับท่าต่อย
- Animator contract ที่โค้ดคาดหวัง:
  - Float `MoveSpeed`
  - Float `MoveX` (ซ้าย/ขวา) และ `MoveZ` (หน้า/หลัง) สำหรับท่าเดินผสมทิศทาง
  - Float `TurnAmount` (-1 ซ้าย / 0 ตรง / 1 ขวา) และ `TurnBlend` (น้ำหนักผสมท่า Turn)
  - Trigger `Dash`
  - Bool `HasGun`
  - Trigger `Shoot`
  - Layer ชื่อ `WeaponUpperBody`
  - Layer ชื่อ `MeleeUpperBody` สำหรับท่าถือ `Sword_Idle` ใช้ upper-body mask เดียวกับปืน และปิดระหว่างโจมตี/หยิบ/dash
  - State `Base Layer.UnarmedPunch` (Jab) และ `Base Layer.UnarmedPunchCross` (Cross) ที่กลับ `Locomotion` หลังเล่นจบ (เรียกผ่าน `CrossFadeInFixedTime` ไม่ต้องมี trigger เพิ่ม)
  - State `Base Layer.MeleeWeaponAttack` และ `Base Layer.Pickup` สำหรับ `Sword_Attack` และ `PickUp_Table`
- `MoveSpeed` ใน Blend Tree ใช้ `0 = Idle`, `0.25 = Walk`, `0.65 = Run`, `1 = Sprint`; `PlayerAnimationController` map ความเร็วจริงจาก `CharacterController` เทียบกับ `WalkSpeed`/`RunSpeed`/`SprintSpeed` แบบต่อเนื่อง จึงยังผสมจาก Sprint ลง Run/Walk ตามความเร็วที่ผ่อน ไม่ตัด gait ทันทีที่ปล่อย Shift

### Locomotion Walk / Run / Sprint + Turn ของ Kevin Iglesias (24 ตุลาคม 2026)

- `Base Layer.Locomotion` ใช้ tree `Locomotion + Turn` แบบ 1D ด้วย `TurnBlend`: 0 → tree `Gait Speed`, 1 → tree `Turn Left - Idle - Turn Right` ทำให้ท่า Turn ผสมกับการเคลื่อนที่ ไม่ใช่ state แยกที่ตัดท่าวิ่ง
- `Gait Speed` ใช้ `MoveSpeed` ผสม Idle Kevin (`HumanM@Idle01`) → `Walk 8 Directions` → `Run 8 Directions` → `Sprint Directions` ตาม threshold 0 / .25 / .65 / 1
- Walk/Run/Sprint เป็น `2D Freeform Directional` ใช้ `MoveX` / `MoveZ` มี Idle ที่ `(0,0)` ทิศตรงรัศมี 1 ทิศเฉียง `(±0.707, ±0.707)` Walk/Run มีครบ 8 ทิศ Sprint ในแพ็กมี 5 ทิศ (หน้า/เฉียงหน้า/ซ้าย/ขวา) จึงใช้ Run ถอยหลัง/เฉียงหลัง 3 ทิศเป็น fallback ที่ timeScale 1.125 ไม่ mirror ท่าวิ่งหน้าไปถอย
- สำเนาที่ใช้จริงอยู่ใน `Assets/_Project/Animations/Player/Locomotion/`: Idle, Walk (loop .8 s), Run (loop .6 s), Sprint (loopประมาณ .533 s), Turn_Left/Turn_Right (loop 1 s) มาจาก FBX รุ่น In-place ปัจจุบันย้ายมาอยู่ใน `Assets/Assets_Imported/Kevin Iglesias/Human Animations/Animations/Male/` ไม่ใช้รุ่น `[RM]` และยังปิด root motion ให้ CharacterController เคลื่อนที่
- สำเนาคลิปเพิ่ม `Orientation Offset Y` 180° ให้ตรงกับ yaw 180° ของ Space Soldier เดิม ไม่แก้คลิปต้นฉบับ Kevin ไม่เปลี่ยนจุดจับปืน/กระบองหรือการหันของโมเดล
- `PlayerAnimationController` ใช้ความเร็วเคลื่อนที่จริงบนพื้นและแปลงเป็นทิศ local ของ root Player (ไม่ใช่แกน CharacterModel ที่มี yaw ชดเชย) จึงยังหันหน้าเล็งแล้วเดินข้าง/ถอยได้ และเมื่อชนจนหยุดจะไม่เล่นท่าเดินจาก input ที่ยังกดค้างอย่างเดียว
- `Direction Damp Time = 0.12 s`, `Damp Time = 0.1 s`, `Turn Damp Time = 0.08 s` ปรับได้ใน PlayerAnimationController ทำงาน Update ลำดับ 100 หลัง movement/combat-facing และก่อน Animator ประเมิน pose
- Turn อ่าน angular velocity ของการหันจริงด้วย `DeltaAngle` (ข้าม 0/360° ได้) เต็มที่เมื่อหมุน `240°/s` ลดน้ำหนักตามความเร็ววิ่งจนเป็น 0 ที่ Run เต็มความเร็ว เพื่อไม่ใช้ท่าเท้าปักขณะ Sprint ปิด Turn overlay ระหว่างถือปืน/โจมตี/dash/หยิบ/ปิด movement; ทิศทาง gait ยังทำงานขณะเล็ง
- ไม่ยิงจะหันตามการเดิน; ขณะกดโจมตีระยะไกลและช่วง aim hold ให้ทิศเล็งมีสิทธิ์เหนือการเดิน โดย gait ยังคำนวณทิศข้าง/เฉียง/ถอยจากความเร็วจริงเทียบกับ root ไม่ใช่ระบบเล็งเมาส์หรือล็อก strafe ตลอดเวลา
- ไม่อ่านความเร็ว dash/shadow dash/หยิบของหรือ movement ที่ถูกปิดมาขับ tree เดิน และยังเก็บท่าหมัด ฟาดอาวุธ Pickup และ layer ปืน/กระบองเดิมครบ
- `PlayerLocomotionSpineProxy` บน CharacterModel ใช้ `Rig/B-root/B-spineProxy` แยกการบิดสะโพกออกจากท่าถืออาวุธ ทำงาน LateUpdate ลำดับ 150 ใช้ yaw correction 180° และน้ำหนัก weapon layer ขณะอยู่ Locomotion ทุก gait (รวม Idle/Sprint/Turn เพราะตอนนี้เป็น Kevin ทั้งหมด) ไม่แก้ Spine ระหว่าง Dash, หมัด, ฟาด หรือหยิบของ
- `HumanMalePlayer.overrideController` ปัจจุบัน (และ `SpaceSoldierPlayer.overrideController` สำหรับร่างเก่า) ใช้สำเนาท่า `Locomotion/Rifle_Aiming_Idle_Locomotion.anim` และ `Rifle_Aiming_Shoot_Locomotion.anim` ซึ่งเพิ่ม quaternion tracks ของ `Rig/B-root/B-spineProxy` จากท่า Spine เดิม เพื่อให้ปืนยังชี้ไปหน้าตัวละครเมื่อเดินข้าง/ถอย ไม่แก้ animation ต้นฉบับ Soldier
- ตรวจใน preview scene ผ่าน: เลือกคลิปทุก gait/8 ทิศ, ผสมทิศและผสม gait, Turn ซ้าย/ขวาผสมการเคลื่อนที่, direction damping/pause, ปืนเล็ง/ยิงชี้หน้าทุก gait, tap อยู่ใน Walk, เร่งถึง Run/Sprint, กลับตัวไม่ snap, ผ่อนจนหยุด, ทแยงไม่เร็วขึ้น, acceleration สอดคล้อง 30/60/120 FPS, Jab/Cross/Pickup/ตีอาวุธทีเดียวตาย รายงาน local `Temp/KevinGaitsVerification.txt`
- ทดสอบ Play Mode ของ WorldPrototype ด้วย input จำลองผ่าน Input System จริง: พบคลิป Walk, Run, Sprint, Turn และหลายคลิป blend พร้อมกันทั้งหมด ความเร็วจริงถึง Run 5 / Sprint 10 และปล่อยแล้วหยุดที่ 0 รายงาน `Temp/KevinGaitsLiveCheck.txt` (`ALL LIVE CHECKS PASSED`) นำตัวจำลอง input ออกหลังทดสอบ ไม่เปลี่ยน input binding ของผู้เล่น
- สำรองก่อนแก้ไว้ใน `Logs/AnimationBackups/PlayerAnimator_BeforeKevinGaits.controller` และ `WorldPrototype_BeforeKevinGaits.unity` รวมสำรองรอบ Walk ก่อนหน้า `*BeforeDirectionalWalk*` (local ไม่ track ใน Git) ต้อง sync รายการคลิปใน `SpaceSoldierPlayer.overrideController` เมื่อเปลี่ยน Blend Tree เพื่อให้ตรงกับ controller โดยคง override ปืนเดิม; สคริปต์สร้าง/ทดสอบชั่วคราวนำออกเมื่อจบงาน

### โมเดลปัจจุบัน HumanMale_Character_Free และรูปลักษณ์ (24 ตุลาคม 2026)

- ทั้ง `WorldPrototype` และ `PrototypeArena` ใช้ `Assets/_Project/Prefabs/Player/HumanMalePlayerVisual.prefab` ใต้ `Player/Visual/CharacterModel` เป็น variant ของ `Assets/Assets_Imported/Blink/Art/Characters/Stylized/Humans/Prefabs_Humans/HumanMale_Character_Free.prefab` ไม่แก้ prefab/FBX ต้นฉบับและไม่ลบ Space Soldier asset เก่า
- ใช้ Humanoid avatar `HumanMale_CharacterAvatar` ของร่างใหม่ ปิด root motion และคง CharacterModel local yaw 180° ให้ตรงคลิป Locomotion ที่ชดเชยแกนไว้แล้ว คง root Player, CharacterController, stats, input, กล้อง, หมัด, Pickup, อาวุธประชิด 2 ช่องและ HUD เดิม
- `Assets/_Project/Animations/Player/HumanMalePlayer.overrideController` สืบทอด `PlayerAnimator.controller` และคงท่าเล็ง/ยิง Rifle เดิม (`Rifle_Aiming_*_Locomotion`) ไม่มีการเปลี่ยน damage 20 / projectile speed 18
- ปืนยังเป็น `Weapon_R` จาก Space Soldier พร้อม mesh/material และ `MuzzlePoint` เดิม ย้ายมาใต้ `GunGripSocket` ของ RightHand ร่างใหม่ ปรับขนาดปืน 0.84 ให้เข้ากับสัดส่วนคน และปรับจุดจับจาก pose เล็งจริง ไม่ใช้แกนมือ bind pose ของสอง rig แบบตรง ๆ
- `PlayerRifleHandIK` บน CharacterModel ทำงาน LateUpdate ลำดับ 175 หลัง `PlayerLocomotionSpineProxy` (150): ใช้ `RifleRightHandTarget` ใต้ torso ช่วยวางปืนใกล้กลางลำตัว และ `RifleSupportHandTarget` ใต้ปืนให้มือซ้ายรองปืน ใช้ two-bone rotation IK จำกัดความยาวแขน ไม่ยืดกระดูก และใช้ wrist rotation ของ animation เป็นฐานก่อนเพิ่ม correction เล็งจริงตามหัวข้อ Combat ทำงานเฉพาะตอนปืน active และ upper-body gun layer มีน้ำหนัก จึงไม่ทับหมัด/ฟาด/Pickup/Dash
- `MeleeWeaponSocket` ผูก RightHand ใหม่; `MeleeBackSocket` ผูก UpperChest/Chest ใหม่ ขยับหลัง 0.03 m บวก offset ของ weapon definition เดิม ยังสะพายเฉียง/ไขว้สองชิ้นและชักเฉพาะศัตรูประชิด
- ตัว HumanMale ที่ผู้ใช้วางเปรียบเทียบบน root ของ WorldPrototype ยังคงอยู่ แยกจาก Player ที่ควบคุมจริง สำรองฉากก่อนเปลี่ยนรวม unsaved edits ไว้ใน `Logs/SceneBackups/WorldPrototype_BeforeHumanMale.unity` และ `PrototypeArena_BeforeHumanMale.unity`

#### ระบบเตรียมปรับสีและทรงผม

- `PlayerAppearance` อยู่บน CharacterModel เป็น `ExecuteAlways` ดูสี/ทรงผมจาก Inspector ได้ทั้ง Edit/Play mode ปรับผ่าน `Current Appearance` โดยยังไม่มีหน้าจอ Character Creation/ปรับรูปลักษณ์ (หน้า Inventory ปัจจุบันดูของและเลือกอาวุธเท่านั้น) และยังไม่เขียน profile ลงดิสก์อัตโนมัติ
- `PlayerAppearanceData` เป็น struct serialize ได้ มี `skinColor`, `hairColor`, `hairStyleId` ใช้ `CurrentAppearance` อ่าน snapshot และ `ApplyAppearance(data)` คืนค่าจาก profile ในอนาคตได้ ชื่อทรงผมใช้ ID คงที่ ไม่ใช้ index ที่เปลี่ยนเมื่อเรียงลิสต์ใหม่
- มี ID `bald` (ปิด Hair2/TopKnot), `short` (Hair2), `topknot` (Hair2 + TopKnot; default) ผมเป็นชิ้นส่วน skinned ที่มีอยู่แล้ว ไม่สร้างโมเดลใหม่ทุกครั้งที่เปลี่ยน สามารถเพิ่ม option หลายชิ้นใน `Hair Styles` ได้ โดยชิ้นใหม่ต้อง bind กับ skeleton ของร่างนี้
- `SetSkinColor(Color)` เปลี่ยนเฉพาะ material slots ผิว Body และ Face1 ทั้งส่วน active/inactive; `SetHairColor(Color)` เปลี่ยนผม หนวด/เครา และคิ้ว ไม่เปลี่ยนดวงตา เกราะ ยางมัดผมหรือลูกปัด (slot ของ Accessories ถูกแยกออก)
- Material copies อยู่ใน `Assets/_Project/Materials/PlayerHuman/` ใช้ URP/Lit และ texture เดิม slot ผิว/ผมตั้ง base tint เป็นขาวแล้วให้ appearance กำหนดสี ใช้ `MaterialPropertyBlock` แยกต่อ renderer/material slot จึงไม่แก้ shared material และไม่เปลี่ยนสีตัวละครอื่น ค่า RGB จำกัด 0..1, alpha 1
- API: `TrySetHairStyle(id)` / `ApplyAppearance(data)` คืน false หาก ID ไม่รู้จัก โดยไม่เปลี่ยนค่าบางส่วน; `HairStyleCount`, `GetHairStyleId(index)`, `GetHairStyleName(index)` ใช้เติมรายการใน UI; event `AppearanceChanged` ใช้ให้ระบบ profile/UI ติดตามค่า
- ผมสลับด้วย GameObject active state เท่านั้น ไม่เปลี่ยน `Renderer.enabled` ที่ระบบ Shadow Dash ควบคุม จึงไม่ทำให้ผมโผล่ขณะล่องเงา และเมื่อออก Shadow Dash ยังคงทรงที่เลือก

ตัวอย่างต่อเข้ากับหน้าปรับแต่ง/ระบบเซฟในอนาคต:

```csharp
PlayerAppearance look = player.GetComponentInChildren<PlayerAppearance>(true);
look.SetSkinColor(new Color(0.7f, 0.4f, 0.25f));
look.SetHairColor(new Color(0.12f, 0.04f, 0.02f));
look.TrySetHairStyle("short");
string json = JsonUtility.ToJson(look.CurrentAppearance);
bool restored = look.ApplyAppearance(JsonUtility.FromJson<PlayerAppearanceData>(json));
```

- ตรวจด้วย Animator/preview scene: avatar/renderer bone refs, แนวปืนทุก gait/ทิศ, มือจับปืน, ผิว/ผมลงเฉพาะ slots, สีแยกสอง instance, JSON round-trip, ID ไม่ถูกต้อง, สลับผมระหว่าง Shadow Dash, Jab/Cross/Pickup/one-hit และ stats ปืนเดิม รายงาน `Temp/HumanPlayerVerification.txt`; ภาพตรวจใน `Temp/HumanPlayer_*.png` สคริปต์สร้าง/ทดสอบชั่วคราวนำออกก่อนส่งงาน
- Play Mode ผ่านด้วย Player จริง: input โจมตี J สร้าง projectile ตรง MuzzlePoint, HP ศัตรู 100 → 80, Run/Sprint, ปืนชี้ตามการหัน, ปรับสี/ทรงผมระหว่างเล่น และมือซ้ายตรง support target (error < 0.05 m) รายงาน `Temp/HumanPlayerLiveCheck.txt` ไม่เก็บศัตรู/input จำลองหรือค่ารูปลักษณ์ทดสอบไว้ในฉากหลังออก Play Mode; C# build ผ่าน 0 warnings / 0 errors

### โมเดล Space Soldier เดิม (ประวัติก่อนเปลี่ยน HumanMale)

- ทั้ง `WorldPrototype` และ `PrototypeArena` ใช้ `Assets/_Project/Prefabs/Player/SpaceSoldierPlayerVisual.prefab` เป็น prefab variant ของ `Assets/SciFi_Space_Soldier/Prefabs/Space_Soldier_Ragdoll_A.prefab` ใต้ `Player/Visual/CharacterModel` ไม่เปลี่ยน root Player หรือค่าการควบคุม
- Avatar เป็น Humanoid ใช้ Locomotion จาก Kevin และ retarget roll หมัด Jab/Cross เก็บของ/ฟาดกระบองจาก UAL1 เดิม ไม่ต้องใช้ parameter/controller เดโมของ Soldier แทนระบบเกม
- ใช้ `SpaceSoldierPlayer.overrideController` สืบทอด `PlayerAnimator.controller` และ override เฉพาะ GunIdle/GunShoot ด้วยสำเนาท่า `Rifle_Aiming_Idle`/`Rifle_Aiming_Shoot` ที่เพิ่ม spine-proxy tracks สำหรับเดิน (ดูหัวข้อก่อนหน้า); ชุดต้นฉบับยังมี `Rifle_Idle`, `Rifle_Reload`, walk/run/sprint แบบ Rifle, `PickupObject`, `Roll`, `Death_A` ให้ใช้ต่อ แต่ยังไม่ได้เชื่อมระบบกระสุน/รีโหลดหรือ ragdoll ตอนตาย
- คงการหมุน local Y `180°` ของ CharacterModel เพื่อให้แกนท่าฐาน UAL1 ตรงกับทิศ Player ถ้าเปลี่ยนเป็น `0°` จะทำให้ทิศเล็งและปลายปืนกลับไปด้านหลัง
- ปืนที่เรียกใช้เป็น Rifle ที่ติดกับโมเดลอยู่แล้ว (`Weapon_R` ใต้มือขวา) จุด `MuzzlePoint` มาจากจุดปลายกระบอก `ShootFX` ของชุดเดิม `PlayerRangedWeapon.muzzlePoint`, `PlayerWeaponVisual.gunVisual` และ `PlayerAnimationController.animator` อ้างอิงร่างใหม่นี้แล้ว; damage `20` / speed `18` ยังเดิม
- `PlayerMeleeEquipment.weaponHand` อ้างอิง `MeleeWeaponSocket` ใต้กระดูก RightHand มี offset กลางฝ่ามือและแกนที่ชดเชย rig ใหม่ เพื่อใช้ Grip ของอาวุธเดิมได้ `weaponBack` อ้างอิง `MeleeBackSocket` ใต้ Chest ซึ่งขยับออกจากเกราะหลังเล็กน้อยเพื่อไม่ให้โมเดลอาวุธจมในเกราะ
- ปิด root motion และตั้ง ragdoll Rigidbody ให้ kinematic/no gravity พร้อมปิด collider ที่กระดูกขณะควบคุม Player ใช้ CharacterController เดิมเคลื่อนที่; ยังไม่เปิด ragdoll อัตโนมัติตอนตาย
- Material ร่าง/มือ/ปืนเป็นสำเนา URP Lit ใน `Assets/_Project/Materials/PlayerSoldier` ไม่พึ่ง surface shader แบบ Built-in ของแพ็กเกจ; โมเดล/texture ต้นฉบับยังอยู่ครบ
- สำรองฉากก่อนเปลี่ยนรวมการแก้ใน Unity ที่ยังไม่บันทึกไว้ใน `Logs/SceneBackups/WorldPrototype_BeforeSpaceSoldier.unity` และ `PrototypeArena_BeforeSpaceSoldier.unity` (ไฟล์สำรอง local ไม่ track ใน Git) โดยเก็บวัตถุทดสอบที่คุณวางไว้ในฉากเดิม

### Health และ Game Over

- Player HP สูงสุด `100`
- Shadow Dash ทำให้ `PlayerHealth.IsInvulnerable` เป็น true
- เมื่อตายจะ disable `PlayerMovement` และ `PlayerAttackController`
- `GameOverUI` เปิด panel และขอพักด้วย `GamePauseReason.GameOver` ผ่าน `GamePauseController`; เวลาหยุด เสียงพัก และ cursor ใช้งาน UI ได้ การปิดกระเป๋าไม่สามารถยกเลิกเหตุผลพักจาก Game Over
- Restart ล้างเหตุผลพักและโหลด active scene ใหม่; ใน Editor รองรับฉากที่ยังไม่อยู่ใน Build Settings ด้วย แต่ต้องเพิ่ม `WorldPrototype` ก่อนแจก build จริง

## 6. Enemy systems

Prefab enemy หลักสืบทอดโครงจาก `EnemyBase.prefab` ซึ่งคาดหวัง component ชุดนี้บน root:

- `EnemyHealth`
- `Targetable`
- `NavMeshAgent`
- `EnemyAI`
- `EnemyAnimationController`
- `EnemyAttack`
- `EnemyDeathController`

ค่าฐาน:

- HP `100`
- NavMeshAgent speed `3.5`
- detection range `12 m`, มุมมองด้านหน้า `110°`
- lose target range `16 m` เป็นระยะมองต่อเมื่อเคยพบแล้ว ไม่ใช่การรู้ตำแหน่งรอบตัว
- patrol speed `1.8 m/s`, patrol radius รอบจุดเกิด `7 m`, พักแต่ละจุด `0.6–1.4 s`
- chase speed ใช้ค่าเดิมของแต่ละ prefab (Green `4.2`, Red `3.3`, Blue `20`, Puglin `8`); `EnemyAI.chaseSpeed = 0` หมายถึงใช้ค่าเดิมจาก NavMeshAgent ไม่ได้แปลว่าหยุด
- attack range `1.6 m`; stopping distance ตอนวิ่งไล่ใช้ 80% ของระยะโจมตี เพื่อไม่ค้างอยู่นอกระยะตีเล็กน้อย
- damage `10`
- hit delay `0.35 s`
- attack cooldown `1.2 s`
- หลังตายอยู่ต่อ `3 s` เพื่อเล่น animation แล้วจึง Destroy

AI behavior — frontal sight + memory (25 ตุลาคม 2026):

1. หา GameObject tag `Player` แต่ reference นี้ไม่ใช่การเห็น; เกิดแล้วเริ่ม `Patrol` สุ่มจุดที่มีเส้นทาง NavMesh สมบูรณ์รอบจุดเกิด เดินแม้ยังไม่มี Player
2. ต้องอยู่ในระยะและกรวยด้านหน้า และมี line-of-sight จากระดับตาไปกลางตัว Player จึงเปลี่ยนเป็น `Chase` ตรวจกรวยเดียวกันทุกสถานะ ไม่มีมองรอบตัว 360° ตอน alert
3. ตำแหน่งปัจจุบันถูกคัดลอกไป `LastSeenPosition` เฉพาะเฟรมที่มองเห็นจริงเท่านั้น วิ่งอ้อมด้านหลังหรือหลังกำแพงแล้วไม่ลืมทันที แต่ไม่อัปเดตตำแหน่งเราแบบทะลุกำแพง
4. คลาดสายตา/หนีเกินระยะมองต่อ → `Investigate` วิ่งไปจุดที่เห็นล่าสุด ไม่เกิน `investigateDuration = 4 s`; ถึงจุดนั้นหรือไปไม่ได้แล้ว → `Search`
5. `Search` หันมองและเดินตรวจจุดเล็ก ๆ รอบตำแหน่งล่าสุด (`searchRadius = 2.5 m`) เป็นเวลา `searchDuration = 5 s`; เห็นใหม่ก็ไล่ทันที ถ้าไม่เห็นจึงลืม → `Return` กลับบริเวณเกิด → `Patrol` เวลารวมหลังคลาดสายตาได้สูงสุดประมาณ 9 วินาทีก่อนลืม
6. การมองตรวจ solid Collider; trigger/VFX/ศัตรูตัวอื่นไม่บังสายตา ต้นไม้บังด้วย collider ของลำต้น ไม่ใช่ใบไม้ที่ไม่มี collider; สิ่งกีดขวางใหม่ต้องมี collider และ bake NavMesh ใหม่ หรือใช้ carving NavMeshObstacle สำหรับของเคลื่อนที่
7. `EnemyAttack` ตรวจระยะ/สายตา/มุมด้านหน้าใหม่ที่เฟรมสร้าง damage จึงไม่ตีผ่านกำแพงที่เข้ามาบังระหว่าง wind-up; damage/cooldown/ท่าโจมตีเดิมยังอยู่

Animation และการปรับแต่ง:

- `EnemyAnimator.controller` ใช้ 1D Blend Tree `Speed`: Idle `0`, `UAL1 Walk_Loop` `0.5`, `UAL1 Sprint_Loop` `1` ใช้คลิป Humanoid ที่วนได้จริง ไม่ใช้ `__preview__` หรือแก้ FBX ต้นฉบับ
- `EnemyAnimationController` แปลงความเร็วจริงเทียบกับความเร็วเดิน/วิ่งคงที่ของ AI ไม่หารด้วย `agent.speed` ที่เปลี่ยนตาม state จึงไม่เล่นท่าวิ่งขณะลาดตระเวน
- Root ของ Enemy เคลื่อนด้วย NavMeshAgent จึงต้องใช้ Rigidbody แบบ kinematic + `Interpolation = None` (กำหนดทั้ง EnemyBase prefab และป้องกันซ้ำใน `EnemyAI.Awake`) ไม่ใช้ physics interpolation มาดึง Transform กลับ ขณะใช้ Interpolate เดิม วัดความเร็วเคลื่อนจริงช่วงเริ่มไล่ได้เพียงประมาณ `1.23 m/s` แม้ agent รายงาน `4.2`; หลังแก้ วัดตอนวิ่งเต็มได้ `4.20 m/s` ตรงค่าที่ตั้ง ระบบ collider/kinematic ยังเดิม
- ปรับค่าจาก component `EnemyAI` บน Enemy prefab; Inspector ขณะ Play แสดง State, Can See, Memory และตำแหน่งที่เห็นล่าสุด เลือก Enemy ใน Scene View จะแสดงกรวยการมอง/วงลาดตระเวน/จุดจำ
- Timer ใช้ scaled game time จึงพักพร้อม Inventory; ปิด/ตายแล้วหยุด agent อย่างปลอดภัย ไม่มีการแก้ Terrain, spawn marker, wave configuration หรือ Player
- เมนู `Tools > Enemies > Run Awareness Regression` ทดสอบ prefab ทั้ง 4 ตัวบน NavMesh ชั่วคราวแยกจากแผนที่จริง รายงาน `Logs/EnemyAwarenessPlaytest.txt`; `Inspect Awareness Setup` รายงานการเชื่อม clip/Avatar/speed ที่ `Logs/EnemyAwarenessSetup.txt` ไม่ต้องรันตัวติดตั้งซ้ำเพื่อเล่นเกม
- ผลตรวจวันที่ 25 ตุลาคม 2026: Awareness ผ่าน `43` ข้อ (รวมระยะ 12/16 m, อ้อมหลัง, กำแพง/ลำต้น, last-seen ไม่ติดตามผู้เล่นที่ซ่อน, ค้นหา/ลืม/เห็นใหม่, Animation กระดูกจริงทั้ง 4 ตัว, ความเร็วจากระยะเคลื่อนจริงทั้งช่วงเร่งและวิ่งเต็ม ไม่ใช่เช็กเฉพาะค่า agent, Pause, ตีผ่านสิ่งบังไม่ได้ และตาย); Playtest ฉาก WorldPrototype ผ่านอีก `24` ข้อ รวมเดิน/Dash/ยิงศัตรู/Pause/โหลดฉากใหม่ รายงาน `Logs/SummoningPlaytest.txt`; C# build 0 warnings/errors ไม่มี fixture ถูกบันทึกลงฉาก

เมื่อตาย ระบบจะปิด AI, attack, animation controller, NavMeshAgent, Targetable และ collider ทุกตัว ก่อน Destroy ภายหลัง

### Spider และ Bear (25 ตุลาคม 2026)

- Prefab ใหม่ `Assets/_Project/Prefabs/Enemies/Enemy_Spider.prefab` และ `Enemy_Bear.prefab` เป็น variant ของ EnemyBase ใช้ AI มองด้านหน้า/ความจำ/ค้นหา/ลาดตระเวน และระบบ damage/targeting/wave เดิม
- โมเดลจาก `Assets/Spiders/Prefabs/Black Widow.prefab` และ `Assets/Blink/Art/Animals/Stylized/Bear/Bear_Prefabs/Bear_4.prefab`; ทั้งคู่เป็น Generic rig จึงใช้คลิปของตัวเอง ไม่ใช้ท่า Humanoid ของ Imp
- Controller/คลิป/URP materials ที่ปรับแต่งเองอยู่ `Assets/_Project/Enemies/Creatures/`; ไม่แก้ prefab, FBX, animation หรือ material ต้นฉบับ ทั้งสองโมเดลหัน -Z จึงหมุน `ModelRoot/CreatureVisual` 180° ให้ตรงกับทิศเดิน/มอง/ตี +Z; Animator ไม่ใช้ root motion
- Spider: HP `80`, เดิน `1.5`, วิ่งไล่ `4.6 m/s`, ระยะตี `1.6 m`, damage `10`, wind-up ประมาณ `0.585 s`, cooldown ประมาณ `1.583 s`; ไม่มี Run แยกจึงใช้ Walk เร่ง `2.2×` ที่ Speed=1 และ Attack1 `1.8×`
- Bear: HP `280`, เดิน `1.4`, วิ่งไล่ `4 m/s`, ระยะตี `2.2 m`, damage `20`, wind-up `0.51 s`, cooldown `1.8 s`; มี Idle/WalkForward/RunForward/Attack1/Death ของตัวเอง
- แพ็กทั้งคู่ไม่มี AudioSource หรือเสียงติดมา จึงใช้ `Audio/Mobs/Spider/spider-1.mp3`, `spider-2.mp3`, `spider-attack.mp3` และ `Audio/Mobs/Bear/sound.mp3` สำหรับเสียงเดินตรวจพื้นที่/พบเป้าหมาย/ตี/โดนตี/ตาย; Bear มีไฟล์เดียวจึงใช้ซ้ำแต่ไม่ซ้อนคำรามยาวหลายชั้น
- `EnemyVoice` เป็น component เสริมเฉพาะสอง prefab นี้ ปรับคลิป/volume/pitch/ambient interval ได้ใน Inspector; AudioSource อยู่ child `CreatureVoice`, 3D non-looping, ช่วงเสียง 4–25 m, ไม่เล่นทันทีเมื่อเกิด, พักพร้อม Inventory และมีหนึ่งเสียงต่อ creature; `EnemyAttack.AttackStarted` แจ้งเริ่มท่าโดยไม่เปลี่ยนจังหวะ damage เดิม
- หลังตายคงศพอย่างน้อยจนคลิป/เสียงจบ (Spider ประมาณ `3.35 s`, Bear `4.88 s`) แล้วลบ AudioSource ไปพร้อมกัน การตีประชิด one-hit ของ Player ยังฆ่าด้วย HP ที่เหลือและนับ wave ตามเดิม
- ทดสอบผ่าน `39` ข้อใน `Logs/CreaturePlaytest.txt`: Generic กระดูกขยับจริง, walk/run blend และความเร็วจากการเคลื่อนจริง, สายตา/สิ่งบัง/ความจำ, attack wind-up/damage, ข้อมูลเสียงไม่เงียบ/เล่นจริง, death/pause/cleanup และ real wave spawner; เมนู **Tools > Enemies > Run Spider and Bear Regression** ไม่แก้ฉากหรือ NavMesh จริง
- **Validate Spider and Bear** ตรวจ prefab/component/material/voice; **Capture Spider and Bear** สร้างภาพชั่วคราว `Logs/SpiderBear_Preview.png`; installer ป้องกันการเขียนทับและไม่ต้องรันซ้ำเพื่อเล่นเกม

### Waves ใน `WorldPrototype`

| Wave | Enemy |
| --- | --- |
| 1 | Green Imp ×3 |
| 2 | Green Imp ×3, Red Imp ×2 |
| 3 | Green Imp ×4, Blue Imp ×3, Puglin ×1 |
| 4 | Spider ×3, Bear ×1 |

- เริ่ม Wave แรกหลัง `2 s`
- เว้นการ spawn แต่ละตัว `0.5 s`
- เริ่ม wave ถัดไปต่อเมื่อศัตรูของ wave ปัจจุบันตายหมด
- จุด spawn ต้องอยู่ห่าง Player อย่างน้อย `7 m` และหา NavMesh ได้ในรัศมี `2 m`
- มี event `WaveStarted`, `EnemyCountChanged`, `AllWavesCompleted` พร้อมต่อ UI/ระบบชนะ แต่ยังไม่มี Victory UI หรือ wave HUD ที่ใช้งาน event เหล่านี้

## 7. Interaction, UI และ Camera

### แผนที่ป่าเต็มพื้นที่ (26 ตุลาคม 2026)

- ฉาก WorldPrototype ใช้พื้นที่เดิม 200 × 200 m: จุดเริ่ม/แท่นอัญเชิญซ้ายล่าง, หมู่บ้านซ้ายบน, เมืองขวาล่าง, ปราสาทจอมมารขวาบน และแคมป์กลางป่า
- มีทางคดเคี้ยว 7 เส้น: ทางหลักกว้างประมาณ 6–7 m และทางลัดผ่านป่ากว้าง 4.5 m; ระยะเผื่อต้นไม้ให้เดิน วิ่ง หลบ และต่อสู้ได้ ถนนเป็น terrain paint ไม่ใช่ถนนตรงเป็นตาราง
- Hierarchy `World/Map_WindingForest` แยก Settlements / Forest / RocksAndUndergrowth / TrailLandmarks / Boundary; `ForestMapLayout` เก็บ landmark และ waypoint สำหรับดู/แก้ใน Editor ไม่มี Update หรือระบบ gameplay แอบรัน
- TerrainData ใหม่ `Assets/_Project/World/WindingForestMap/WindingForestTerrain.asset`; Collider ใช้ข้อมูลเดียวกัน เก็บ Terrain และ NavMesh เดิมไว้ ไม่ทับ source pack; บริเวณแท่นและป่าเริ่มต้นคง sculpt/paint เดิมแล้วค่อย blend ออกไปพื้นที่ใหม่
- ใช้บ้าน Quaternius จาก `Assets_Imported/villanger`, ชิ้นหิน KayKit Dungeon จาก `Assets_Imported/city`, หอคอย/หน้าผา Inferno และป่า Fantasy Worlds; สร้าง material URP ของแผนที่เองและรักษาแกน FBX ต้นฉบับ
- อาคารมี footprint collider เป็นลูกของอาคาร; ป่าใหม่มี collider เฉพาะลำต้น ขอบแผนที่มีหน้าผาตกแต่งกับ safety collider ต่อเนื่องทั้งสี่ด้าน รองรับร่างปกติและ PlayerShadow; ไม่ใช้ physics collider ของกิ่ง/ใบมาขวางทาง
- ยังเป็นฉากภายนอก: ไม่มี NPC/ร้านค้า/เควสต์/บอสจริงหรือระบบเข้าอาคาร มีเพียง marker เผื่อทำต่อ; Player, wave ทั้ง 4 ชุดและ marker ศัตรูเดิมไม่ถูกเปลี่ยน
- เอกสารแก้แต่งต่อ: `Assets/_Project/World/WindingForestMap/README.md`; เมนู Tools > World > Full Map มี Focus / Validate / Capture / Run Full Map Playtest และ guard ป้องกัน builder เขียนทับงานตกแต่งภายหลัง
- ย้าย waypoint แล้วถนนไม่ repaint อัตโนมัติ ให้แก้ Terrain/ต้นไม้ตามด้วย และ rebake NavMesh หลังย้ายของที่มี collider
- ผลทดสอบ: Full Map static validation ผ่าน 142 ข้อ, Play Mode ของแผนที่ผ่าน 26 ข้อ และระบบแท่น/Player เดิมผ่าน 24 ข้อ; การเปรียบเทียบ sculpt 22,700 จุด รวม paint/grass ในพื้นที่เริ่มต้นผ่านทั้งหมด ใช้เมนู **Rebake Full Map Navigation** หลังแต่งฉากเพื่อสำรองฉากและสร้าง bake ใหม่โดยไม่ทับไฟล์ก่อนหน้า

Interaction:

- `PlayerInteractor` ค้นหา `Interactable` ที่ใกล้ที่สุดในรัศมี `2.2 m`
- ใช้ layer `Interactable` (layer 8) และ buffer collider 16 รายการ
- `Interactable` เป็น abstract base; ของจริงต้อง implement `Interact(GameObject interactor)`
- `TestInteractable` ปัจจุบันเพียง log ข้อความเพื่อทดสอบ
- `MeleeWeaponPickup` เป็นวัตถุเก็บอาวุธจริง: กด E แล้ว `PlayerInteractor` จองของ เล่น `PickUp_Table` และค่อยย้ายอาวุธเข้าสู่ `PlayerMeleeEquipment` ตอนคลิปถึง 55%
- ขณะหยิบ `IsInteracting` และ `PlayerMovement.IsInteractionLocked` จะหยุดเดิน/วิ่ง/dash และการโจมตี แต่ CharacterController ยังได้รับแรงโน้มถ่วง
- ก่อนถึงจังหวะหยิบ ถ้า Player ตาย, ของถูกปิด/หาย, ออกนอกระยะ หรือ animation ใช้ไม่ได้ จะคืนการควบคุมและไม่กินของ; กด E ซ้ำไม่ทำให้ได้อาวุธซ้ำ
- มีช่องอาวุธประชิด 2 ช่อง เก็บลงช่องว่างแรกและเลือกชิ้นแรกให้อัตโนมัติ เก็บชิ้นที่สองแล้วไม่เปลี่ยนอาวุธที่กำลังเลือก เมื่อครบ 2 ชิ้นจะไม่เก็บชิ้นที่สาม ไม่ลบของในโลกและไม่แทนของเดิม; มีหน้า Inventory แล้ว แต่การทิ้ง, save และระบบของดรอปยังไม่ได้ทำ
- `GetWeapon(0/1)`, `WeaponCount`, `SelectedSlotIndex` และ `TrySelectSlot(0/1)` เป็น API สำหรับสองช่องนี้; `CurrentWeapon` ยังหมายถึงชิ้นที่เลือกและใช้โจมตี `SlotsChanged` แจ้ง UI เมื่อเก็บ/เปลี่ยนช่อง ส่วน `WeaponChanged` แจ้งเมื่ออาวุธที่เลือกเปลี่ยน
- สลับด้วยการคลิกช่องอาวุธใน HUD; ห้ามสลับกลางท่าโจมตี หยิบของ dash/shadow dash หรือหลังตาย เพื่อไม่ให้โมเดลและ damage ของท่าที่กำลังเล่นเปลี่ยนกลางคัน
- `InteractionPromptUI` ฟัง `FocusChanged` และแสดงข้อความแบบ `[E] <prompt>`; label ยังไม่เปลี่ยนตามอุปกรณ์ input

### เพิ่มอาวุธประชิดต้นแบบ

- ตัวอย่างครบชุดอยู่ที่ `Assets/_Project/Prefabs/Weapons/TrainingClub.asset`, `TrainingClub.prefab` (โมเดลถือ) และ `TrainingClubPickup.prefab` (วัตถุในโลก)
- สร้างข้อมูลผ่าน `Create > TopDownShooter > Melee Weapon` แล้วกำหนดชื่อ, held prefab, `Kills In One Hit` (เปิดเป็นค่าเริ่มต้น), damage แบบปกติ, range, hit time และ grip transform เทียบกับกระดูก RightHand
- ปรับ `Back Position`/`Back Euler Angles` ในข้อมูลอาวุธสำหรับตำแหน่งสะพายเทียบกับ back socket; กระบองใช้ `(0.24, -0.45, -0.22)` และหมุน Z `35°` เพื่อวางเฉียงด้านหลัง ไม่ใช้ท่าถือขณะสะพาย
- วัตถุในฉากต้องมี `MeleeWeaponPickup`, อ้างอิงข้อมูลอาวุธ และมี trigger collider บน layer `Interactable` (8)
- Player ในฉากต้องมี `PlayerMeleeEquipment`, `PlayerInteractor`, `PlayerMeleeAttack`, `PlayerAnimationController` และ `PlayerWeaponVisual`; ชุดนี้เชื่อมให้แล้วใน `WorldPrototype`
- โมเดลกระบองปัจจุบันเป็น geometry ต้นแบบ เปลี่ยน held prefab ภายหลังได้โดยใช้ข้อมูลโจมตีเดิม; อาวุธประชิดในรอบนี้ใช้ท่า `Sword_Attack` ร่วมกัน

UI:

- Health UI ฟัง `PlayerHealth.HealthChanged`
- Energy UI ฟัง `PlayerEnergy.EnergyChanged`
- Game Over UI ฟัง `PlayerHealth.Died`
- `InventoryHotbarUI` ใช้ prefab `Assets/_Project/Prefabs/UI/InventoryHotbar.prefab` อยู่ใต้ `WorldPrototype > HUDCanvas` ยึดขอบล่างกลาง ห่างขอบ 24 หน่วยบน Canvas อ้างอิง 1920×1080
- HUD มี 10 ช่อง: 1–8 (`SUPPLY`) อ่าน item stacks จริงจาก `PlayerInventory` แล้ว แต่ยังไม่มี world drop ที่ป้อนของให้, 9–10 เป็นช่องอาวุธประชิด (`MELEE`) พร้อมจำนวน 0/2–2/2 ชื่ออาวุธ ไอคอน และกรอบสีทองของชิ้นที่เลือก; คลิกช่องว่างจะไม่เปลี่ยนการเลือก
- `MeleeWeaponDefinition.HudIcon` รองรับ Sprite สำหรับไอคอนจริง หากยังไม่ใส่จะแสดงรูปกระบองต้นแบบที่ประกอบจาก UI geometry; ไม่ต้องใช้ภาพภายนอกเพื่อให้ระบบทำงาน
- Event-driven UI เหล่านี้ต้อง subscribe/unsubscribe ใน `OnEnable`/`OnDisable` ตาม pattern เดิม

### Inventory และการพักเกม (24 ตุลาคม 2026)

- ติดตั้งใน `WorldPrototype` และ `PrototypeArena`: Player มี `PlayerInventory` + `PlayerInputGate`; HUDCanvas มี `GamePauseController`, `InventoryPanel.prefab` และ hotbar ที่อ่านข้อมูลชุดเดียวกัน
- กด **I / Tab** หรือปุ่มมุมซ้ายล่างเพื่อเปิด; **Escape**, X หรือ I/Tab เพื่อปิด; Gamepad Select เปิด/ปิด และ East ปิด สามารถเลื่อน focus ด้วย UI navigation และกด Submit เพื่อเลือกปุ่ม
- Modal แบ่งช่องของทั่วไป 8 ช่อง, อาวุธประชิด 2 ช่อง และรายละเอียดด้านขวา คลิก/เลื่อน focus เพื่อดูของ แล้วกด **SELECT WEAPON** เพื่อเลือกอาวุธ ช่องว่างหรือของทั่วไปไม่มีปุ่มใช้งานหลอก
- อาวุธยังมาจาก `PlayerMeleeEquipment` เดิม: การเลือกเปลี่ยนเฉพาะชิ้นที่จะใช้อัตโนมัติเมื่อศัตรูประชิด ไม่บังคับถือค้าง ไม่เปลี่ยน one-hit damage และไม่สร้างโมเดลซ้ำ; สลับไม่ได้กลางท่าหยิบ/ตี/dash/shadow หรือหลังตาย
- `ItemDefinition` สร้างผ่าน **Create > TopDownShooter > Inventory Item**: มี stable `ItemId`, ชื่อ, description, Sprite, category (`Resource`, `Consumable`, `Quest`) และ `MaxStack` 1–999 หมวด Consumable เป็นข้อมูลเท่านั้น ยังไม่มีระบบใช้ไอเท็ม
- `PlayerInventory.Capacity = 8`, `GetItem(slot)`, `GetQuantity(slot)`, `OccupiedSlots`, `CanAdd(item, amount)`, `TryAdd(item, amount)`, `TryRemove(slot, amount)`, event `Changed` ทุก mutation ที่สำเร็จแจ้งครั้งเดียว
- Stack รวมเฉพาะ asset เดียวกัน เติมกองเดิมก่อนช่องว่าง และ `TryAdd` เป็น atomic: ถ้าใส่ทั้งหมดไม่พอ คืน false โดยไม่เปลี่ยนจำนวนใด ๆ เหมาะสำหรับให้ระบบ drop ในอนาคตลบของในโลกเฉพาะเมื่อ TryAdd สำเร็จ
- ข้อมูลเป็น runtime memory: เริ่มว่างและรีเซ็ตเมื่อ Restart ยังไม่มี save/load, item drop, use, discard, drag-and-drop, crafting หรือของทดลองค้างในฉาก
- `GamePauseController` เป็นเจ้าของ time/cursor/audio ร่วมกัน ใช้ flag `Inventory` กับ `GameOver` แยกเหตุผล บันทึกค่าเดิมตอนพักครั้งแรกและคืนเมื่อไม่มีเหตุผลเหลือ ไม่สมมติว่า timeScale เดิมต้องเป็น 1; ปิด/disable กระเป๋าไม่ยกเลิก Game Over
- เปิดกระเป๋ากลาง action ได้โดยหยุดเวลาและ Animator ไม่ disable PlayerMovement (เพราะจะทำให้ state ของ dash/Shadow เสีย) หยิบและ melee เก็บ reservation/snapshot เดิมไว้แล้วเล่นต่อหลังปิด; pending ranged shot ถูกยกเลิกอย่างปลอดภัย
- `Menus` action map เปิดคู่กับ `Gameplay` ตลอด ไม่สลับ map เพราะ Interactor ใช้ InputActionReference จาก asset; `PlayerInputGate` กันผู้ใช้ input ทุกจุด ขณะพักไม่เดิน/ยิง/หยิบ/dash และหลังปิดต้องปล่อย **Attack/Dash/ShadowDash/Interact** ก่อนกดใหม่ ส่วนเดินที่กดค้างกลับเดินต่อได้
- Enemy attack ไม่เริ่มใหม่ที่ timeScale 0 และป้องกัน damage จาก coroutine ที่ครบ delay ในเฟรมเดียวกับเปิดกระเป๋า
- UI ใช้ UGUI/TMP ภาษาอังกฤษให้เข้ากับ font ปัจจุบัน แก้หน้าตาได้ที่ `Assets/_Project/Prefabs/UI/InventoryPanel.prefab`; ใส่ไอคอนที่ ItemDefinition หรือ MeleeWeaponDefinition ถ้าไม่มีจะใช้ตัวอักษร/ไอคอนสำรอง
- **Tools > Inventory > Build and Install Inventory** ติดตั้งแบบไม่สร้างซ้ำ ไม่เขียนทับ prefab ที่มีอยู่ และสำรองฉากรวม unsaved edits ก่อนแก้ใน `Logs/SceneBackups/*BeforeInventory*.unity` อย่าลบ prefab เพื่อ rebuild หากปรับหน้าตาไว้แล้ว
- **Tools > Inventory > Run Inventory Regression** เป็นชุดตรวจ Play Mode แบบสั่งรันเอง ใช้ Player/Animator/Input/UI จริง เพิ่ม fixture เฉพาะระหว่างทดสอบ ไม่มีของทดสอบถูก save; รายงาน `Logs/InventoryRegression.txt`, ภาพ `Logs/Inventory_*.png`

Camera:

- `CameraRig` ตาม Player ด้วย `Vector3.SmoothDamp`, smooth time `0.1 s`
- Short Dash เพิ่ม FOV `7` แล้วค่อยคืนด้วย expand speed `18` / recover speed `8`
- Shadow Dash ไม่ทำ FOV punch เพราะ effect ตรวจเฉพาะ `IsDashing`
- `CameraFollow` เพิ่ม Running Bob เฉพาะตอน `IsRunning` และ `CharacterController` เคลื่อนที่จริง; เดิน ยืนกด Shift, dash, shadow dash หรือปิด movement จะค่อย ๆ หยุดโยก
- ค่าเริ่มต้น Running Bob: frequency `2.8 Hz`, amplitude แนวนอน `0.035 m` / แนวตั้ง `0.05 m`, roll `0.2°`, blend speed `8`; ปรับที่ `CameraRig > CameraFollow > Running Bob`
- Running Bob ขยับ/เอียงเฉพาะกล้องลูกจากตำแหน่งเดิม ไม่สะสม offset บน rig และไม่เปลี่ยน FOV; ปิด component แล้วคืนตำแหน่งและมุมกล้องเดิม

## 8. Contracts ที่ห้ามหลุดตอนเพิ่ม Prefab/Scene

### Tag และ Layer

- Player ต้องใช้ tag `Player`
- Enemy collider/root ที่ระบบ targeting ตรวจต้องอยู่ layer `Enemy` (6)
- กำแพง/ฉากที่ใช้ collision convention ปัจจุบันอยู่ layer `Environment` (7)
- Interactable ต้องอยู่ layer `Interactable` (8)
- Shadow state ใช้ layer `PlayerShadow` (9)

### Enemy prefab ใหม่

อย่างน้อยต้องมี component ตาม `EnemyBase`, อยู่บน NavMesh ตอน spawn, มี collider, มี `Targetable.aimPoint` ที่เหมาะสม และมี Animator parameters `Speed`, `Attack`, `Die` ตามที่ enemy scripts เรียกใช้

### Event flow ที่ควรรักษา

- Health scripts เป็นเจ้าของค่า HP และประกาศ event
- Controller ประกาศ intent ผ่าน event (`RangedAttackRequested`, `MeleeAttackRequested`)
- ผู้ทำ damage, animation, facing และ visual เป็น subscriber แยกกัน
- ถ้าเพิ่มเสียง/VFX ให้ subscribe event ที่มีอยู่ เช่น `MeleeHitConfirmed` แทนการยัด dependency เข้า controller

## 9. จุดเสี่ยงและงานที่ควรทำต่อ

ลำดับแนะนำ:

1. เพิ่ม `WorldPrototype` เข้า Build Settings และกำหนดว่าจะเลิกใช้ `SampleScene`/`PrototypeArena` หรือไม่
2. เมื่อเพิ่มระบบใหม่ ให้ Unity import และตรวจ Console รวมถึงการเชื่อม Scene/Prefab ก่อนทดสอบ
3. โซน Forest Demo อบ NavMesh/ตรวจเส้นทาง SpawnPoint ทั้ง 6 จุดแล้ว; เมื่อปั้นพื้นหรือย้ายสิ่งกีดขวางเพิ่ม ให้ bake และตรวจซ้ำ
4. ตัดสินใจ gamepad binding ของ Dash/Interact ที่ชนกัน
5. Playtest transition ระหว่าง run, short dash, shadow dash, attack และ death โดยเฉพาะการกดหลายปุ่มพร้อมกัน
6. ตรวจ Animator layer/parameter และ visual references ของ Player หลัง import
7. เพิ่ม Victory flow จาก `AllWavesCompleted` และ wave/enemy counter หากต้องการ gameplay loop ที่จบสมบูรณ์
8. เพิ่ม line-of-sight หรือ target scoring หากไม่ต้องการล็อกศัตรูหลังกำแพง
9. เพิ่ม object pooling สำหรับ Projectile และ Enemy หากจำนวน spawn สูงขึ้น
10. เพิ่ม PlayMode tests สำหรับ damage, target switching, energy drain/regen และ wave completion

การตรวจสอบล่าสุด (23 ตุลาคม 2026):

- C# build ผ่าน ไม่มี warning หรือ error และ Unity import state ของ `Punch_Jab`/`Punch_Cross` ได้ทั้งคู่
- ทดสอบด้วย Animator จริงใน Unity preview scene แยก: จังหวะก่อนโดนไม่ทำ damage, หนึ่งหมัดทำ damage 15 เพียงครั้งเดียว, ท่าจบแล้วกลับ locomotion, เป้าหมายออกนอกระยะไม่โดน, dash/shadow/death ยกเลิก damage และปิด Animator แล้วไม่ค้างสถานะต่อย
- ตรวจการซ่อนปืนเมื่อเป้าหมายเดิมเข้าประชิด, การปิด upper-body gun pose และการคืนปืนหลังหมัดจบแล้ว
- ตรวจหมัดต่อเนื่อง 4 ครั้งแล้วได้ Jab → Cross → Jab → Cross และหนึ่งหมัดลด HP 15 เพียงครั้งเดียวทุกครั้ง
- ตรวจการเคลื่อนที่ด้วย `CharacterController` จริง: เดินได้ Blend Tree 0.5 และคลิป `Walk_Loop`, วิ่งได้ 1 และคลิป `Sprint_Loop`, หยุดแล้วกลับ Idle
- ตรวจ Running Bob: amplitude ไม่เกินค่าที่ตั้ง, rig และ FOV ไม่ถูกเปลี่ยน, pause หยุดเฟส, เดิน/หยุด/dash/shadow dash หยุดโยก และปิด component แล้วคืนตำแหน่งเดิม
- ผลการตรวจรอบแรกอยู่ใน `Temp/PlayerPunchVerification.txt` และรอบการเคลื่อนไหว/หมัดสลับอยู่ใน `Temp/PlayerMotionVerification.txt` (ไฟล์ชั่วคราว ไม่ได้ track ใน Git)
- ตรวจระบบเก็บรอบแรกใน preview scene แล้ว: ไม่ได้อาวุธก่อนจังหวะหยิบ, กันเก็บซ้ำ, ล็อก/คืนการควบคุม, ยกเลิกก่อนเก็บแล้วของไม่หาย และปืนยังเรียกใช้ได้ทั้งก่อนและหลังเก็บ ผลอยู่ใน `Temp/MeleePickupVerification.txt` (ผล damage 30 ในไฟล์นี้เป็นก่อนปรับ one-hit)
- ตรวจรอบสะพายหลัง/one-hit ด้วย Animator จริง: เก็บแล้วสะพายเฉียง, ถือเฉพาะเมื่อศัตรูประชิด, สลับมือ/หลัง 12 รอบไม่สร้างโมเดลซ้ำหรือตำแหน่งเพี้ยน, ถือปืนแล้วกระบองยังอยู่ด้านหลัง, dash/shadow ซ่อนและคืนอาวุธได้
- ทดสอบกระบองตีศัตรู HP 100 และ 5,000 ตายในครั้งเดียว, event ตายออกครั้งเดียว, ไม่โดนก่อนจังหวะคลิป, ออกห่างก่อนโดนแล้วรอด, dash ยกเลิกการตี, ตีเสร็จสะพายกลับ และหมัดยังลด HP 15 ผลอยู่ใน `Temp/MeleeStowVerification.txt`
- Playtest ใน `WorldPrototype` แล้ว: เดินเข้าหาแท่นใกล้จุดเกิด กด E เล่นท่าหยิบ ของหายจากโลกและกระบองสะพายเฉียงบนหลังโดยมือว่าง
- ตรวจ HUD/สองช่องอาวุธแล้ว: ช่องสำรอง 8 ช่องไม่ถูกเขียนทับ, เก็บชิ้นแรก/ชิ้นที่สองแล้วชื่อ/ไอคอน/ตัวนับอัปเดต, ช่องเต็มแล้วชิ้นที่สามไม่หาย, คลิกสลับอาวุธแล้วกรอบเปลี่ยน, ชิ้นที่ไม่ได้เลือกยังอยู่บนหลัง และสลับกลางท่าไม่ได้ ผลอยู่ใน `Temp/InventoryHotbarVerification.txt`
- Playtest ความรู้สึกในฉากจริงได้ที่ `WorldPrototype`: WASD เดิน, Shift + WASD วิ่งพร้อมกล้องโยก, เข้าใกล้ศัตรูประมาณ 1.8 m แล้วกด/ค้าง `J` เพื่อต่อยสลับ; ปรับ `Hit Normalized Time` และ Running Bob ให้เหมาะกับภาพจริง

การตรวจโมเดลใหม่ (24 ตุลาคม 2026):

- ตรวจ Avatar/กระดูก/Material และข้อมูล clip จริงจาก Unity ผลอยู่ใน `Temp/SpaceSoldierInspection.txt`
- ตรวจจากสำเนา Player ที่เชื่อมจริงในฉาก: ท่าปืนใหม่และจุดยิงอยู่ด้านหน้า, เดิน/วิ่งไม่สลับผิด, หมัด Jab/Cross ลด HP ครั้งละ 15, จังหวะเก็บของ, เก็บ/ถือ/สะพายกระบองสองชิ้น, one-hit melee และซ่อน/คืนร่าง+อาวุธระหว่าง Shadow Dash ผ่าน ผลอยู่ใน `Temp/SpaceSoldierVerification.txt`
- Live Playtest ยิงเป้าชั่วคราวใน WorldPrototype แล้ว: Projectile เริ่มตรง MuzzlePoint ใหม่ (ระยะห่างตอนสร้าง 0), กระสุนโดนและลด HP จาก 100 → 80 ตาม damage 20 ผลอยู่ใน `Temp/SpaceSoldierPlaytest.txt` เป้าทดสอบอยู่เฉพาะ Play Mode ไม่ได้บันทึกลงฉาก
- ตรวจภาพ Play Mode แล้ว: Soldier จับ Rifle ด้วยมือของโมเดลใหม่ และกด E เก็บกระบองแล้ว HUD แสดง 1/2 พร้อมสะพายด้านหลังได้ จากนั้นคืน Editor เป็น Edit Mode และลบสคริปต์ทดสอบชั่วคราวออกแล้ว

ข้อจำกัด:

- มีชุด regression ถาวรแบบ opt-in แล้วที่ `Assets/_Project/Tests` (Combat Aiming, Shadow Trails และ Inventory); diagnostics รุ่นก่อนใน Temp ยังคงเป็นผลตรวจย้อนหลัง ไม่ใช่ Unity Test Runner suite
- ไฟล์ใหม่และการแก้ปัจจุบันยังไม่ commit ควรตรวจ `git status` และ commit เป็นชุดที่อธิบายได้หลัง Playtest ผ่าน

การตรวจ Inventory และการถดถอยล่าสุด (24 ตุลาคม 2026):

- C# build ผ่าน 0 warnings / 0 errors
- `Logs/InventoryRegression.txt`: ผ่าน 59 ข้อ ครอบคลุม stack/atomic add/remove/ช่องเต็ม, UI 10 ช่อง, hotbar sync, เลือกอาวุธ, ปุ่มคีย์บอร์ด/เกมแพด, freeze/resume pickup/melee/short dash/Shadow Dash, input release gate, คืน time/cursor/audio เดิม, disable/re-enable UI, death ระหว่างเปิดกระเป๋า และ Restart ฉากจริงพร้อมตรวจ Ice references หลัง reload
- `Logs/CombatAimingRegression.txt`: รอบสุดท้ายหลังติดตั้ง Inventory และซ่อม Ice reference ผ่าน — กระสุน 37 นัดและ confirmed shot 37 ครั้ง; canceled requests ไม่ยิงตามหลัง และแนวกระสุนตรงกับปลายกระบอกจริง (จำนวนรวมเปลี่ยนเล็กน้อยตาม frame timing)
- `Logs/ShadowDashVfxRegression.txt`: ผ่าน 42 ข้อหลังติดตั้ง Inventory; ยืนยัน Roll → Ice → NinjaJump_Land, Shield_Dash, body/weapon visibility, energy และ cleanup
- พบ `ShadowDashIceForm.headRenderer` ของ WorldPrototype ถูก override เป็น null ตั้งแต่ก่อนติดตั้ง Inventory (ตรวจได้จากไฟล์สำรอง BeforeInventory); เชื่อมกลับเฉพาะหัว Ice ที่มีอยู่เดิมด้วย **Tools > VFX > Repair Missing Ice Head References** และสำรองฉากก่อนแก้ใน `Logs/SceneBackups/*BeforeIceReferenceRepair*.unity` คงสี ขนาด ความเร็ว movement และ override อื่นไว้ทั้งหมด ไม่ต้องติดตั้ง/เขียนทับ VFX prefab ใหม่
- ภาพตรวจ layout ของกระเป๋าว่าง/ของทั่วไป/อาวุธ และ Game Over อยู่ใน `Logs/Inventory_Empty.png`, `Inventory_Supplies.png`, `Inventory_Weapon.png`, `Inventory_GameOver.png`; ของที่เห็นในภาพ Supplies/Weapon เป็น fixture เฉพาะ Play Mode ไม่ใช่ไอเท็มเริ่มต้นที่ save ลงฉาก
- ลอง UI ด้วยเมาส์/คีย์บอร์ดจริงใน WorldPrototype แล้ว: ปุ่มมุมซ้ายล่างเปิดได้, คลิกช่อง 10 เปลี่ยนกรอบ/รายละเอียด, X ปิด, Tab เปิดกลับ และ Escape กลับ gameplay; คืน Unity เป็น Edit Mode หลังตรวจ

### โซนป่าตัวอย่างบนพื้นที่สูง (25 ตุลาคม 2026)

- `WorldPrototype` มี `World/Zone_ForestDemo` รอบ Player ที่โลกประมาณ (-60, 5, -60) ใช้ต้นไม้ หญ้า หิน และ Shader จริงจาก **Fantasy Worlds - DEMO Content / URP**; ก่อนหน้านี้แพ็กยังไม่ได้ import เนื้อหา URP ชั้นในและพื้นทา Soil อย่างเดียว
- ใช้ `Assets/_Project/World/ForestDemo/ForestTerrain.asset` เป็นสำเนาพื้นที่ผู้ใช้ปั้น ทั้ง Terrain และ Collider อ้างอิงสำเนาเดียวกัน; ไม่แก้ heightmap และคง `Assets/New Terrain 1.asset` ต้นฉบับไว้ ทาสี/เติมหญ้าเฉพาะรอบโซนรัศมีไม่เกิน 34m
- ปรับแสงเฉพาะฉาก World และ Local Volume, มี Grass Instancing, พุ่มไม้ และต้นไม้ใหญ่ 18 ต้น; รักษาลานต่อสู้กลางภาพและเก็บ prefab/material ต้นฉบับของแพ็กไว้
- ย้าย Player ลงพื้นและ SpawnPoint เดิมทั้ง 6 จุดรอบลาน อบ NavMesh เป็น asset ใหม่ใน ForestDemo ไม่เขียนทับของเก่า ไม่เปลี่ยน wave count, movement, damage หรือระบบต่อสู้
- **Tools > World > Focus Forest Demo** เปิดมุมตรวจโซน; **Validate Forest Demo** ตรวจ heightmap/Shader/เส้นทางครบ; **Run Forest Playtest** ทดสอบเดิน Dash แปลงร่าง ยิง และ Enemy ไล่ด้วย runtime จริง รายงานใน `Logs/ForestZoneValidation.txt` และ `Logs/ForestZonePlaytest.txt`
- รายละเอียดปรับแต่งและย้อนกลับอยู่ใน `Assets/_Project/World/ForestDemo/README.md`; ยังเป็นเพียงหนึ่งโซน พื้นที่ด้านนอกคงเดิม และต้อง benchmark/ตรวจการบังกล้องก่อนขยายป่าทั้งแผนที่

### แท่นอัญเชิญและจุดเริ่มใหม่ (25 ตุลาคม 2026 — ระยะที่ 1)

- ใช้ฐาน `Temple/CirclePlatformSmall_001` ที่ผู้ใช้วางไว้ต่อ ไม่สร้างซ้ำและไม่เปลี่ยนสเกล/ตำแหน่งโลกเดิม จัดกลุ่มเป็น `World/Zone_ForestDemo/Temple/Zone_SummoningSanctum` เพิ่มเสาโบราณ/เสาหักและแผ่นหินจากแพ็ก Columns ที่ติดตั้งแล้ว พร้อมวัสดุ URP แยก; รูปปั้นและเสาเดิมคงอยู่
- จุดเริ่มปัจจุบันอยู่กลางแท่นประมาณ **(-82.63, 5.778, -82.18)** ใช้ `PlayerSceneSpawnPoint` ใน `Scripts/Core` ทำงานก่อน Movement/Camera และใช้ Player ตัวเดิม ไม่สร้างตัวใหม่ ไม่มีการ reset ค่าสถานะหรือ sequence แสงในรอบนี้
- ใต้ `05_SpawnAndFutureVfx` มี `PlayerSpawnPoint` และ `SummonVfxAnchor` สำหรับระยะถัดไป รอยวงเวทตอนนี้เป็นรอยสลักสีหินไม่เรืองแสง ยังไม่ล็อก input/ซ่อน Player/เพิ่มระบบกันตก
- ย้าย SpawnPoint ของ Wave เดิม 6 จุดมาใกล้แท่นและอบ `World/SummoningSanctum/SummoningNavigation.asset` ใหม่ โดยคงความเร็ว Player, combat, audio และ wave definitions เดิม
- ไม่แก้ sculpt/paint/grass ของ Terrain; พบการทาสีที่ค้างบน GPU ไม่ถูก copy ไปใน native TerrainData clone จึงสำรองสีผ่าน GetAlphamaps และ synchronize/save งานเดิมโดยเทียบก่อน–หลังตรงกันทุกค่า รายงาน `Logs/SummoningTerrainPaintPreservation.txt` และสำเนาครบ `Terrain_UserPaintBackup.asset` (อย่าใช้ `Terrain_BeforeSummoning.asset` รอบแรกกู้สีพื้น)
- `Logs/SummoningPlaytest.txt` ผ่าน 24 ข้อรวมการเดินขึ้นลง, Dash, Shadow, Enemy, กระสุน damage, Inventory pause และโหลดฉากจริงใหม่แล้วกลับมาเกิดบนแท่น; `Logs/SummoningValidation.txt` ตรวจเส้นทางครบทุก SpawnPoint และ Shader
- เปิดมุมดูด้วย **Tools > World > Summoning > Focus Platform**; วิธีปรับ Marker/วัสดุ/สำรองอยู่ใน `Assets/_Project/World/SummoningSanctum/README.md`

### วงเวทเกิด Magic Effects FREE (25 ตุลาคม 2026 — ระยะที่ 2)

- ใช้ `Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab` จริง ลายอักษร/วงแหวน/เส้นพลัง/ประกายตรงกับภาพอ้างอิง สร้างสำเนา `World/SummoningSanctum/MagicCircle/PlayerSummonCircle.prefab` และวัสดุ URP แยก ไม่แก้แพ็กต้นฉบับ
- ใส่ใต้ `05_SpawnAndFutureVfx/SummonVfxAnchor` ใน WorldPrototype เล่นครั้งเดียวตอนเริ่มฉากหรือโหลดฉากใหม่ ไม่ผูกกับ utility `PlacePlayer()` จึงไม่เล่นซ้ำจากการทดสอบ/ย้ายตัว
- `SummoningCircleVfx.cs`: Fade-in 0.65s / Hold 2s / Fade-out 1.2s; สีฟ้า, Radius 2.3m, Brightness 2 และไฟพื้นปรับได้ใน Inspector เก็บ motion ของ Particle เดิม แต่ปิด emission เมื่อเริ่มจางและลบเอฟเฟกต์/ไฟหลังจบ
- ใช้ MaterialPropertyBlock และ shader `SummoningCircleURP.shader` รองรับหลาย instance หลายสีโดยไม่เขียน shared material; ไม่สร้าง material instance รั่วต่อเฟรม ไม่มี Collider/trigger, อยู่ Ignore Raycast และไม่เปลี่ยน Player/Enemy/คลื่นเกิด/NavMesh/Terrain/ไฟเดิมหรือ post-processing ทั้งฉาก
- Player เริ่มและขยับได้ปกติ ยังไม่ซ่อนร่าง/เพิ่มแอนิเมชันอัญเชิญ/ล็อกควบคุม/ให้ invulnerability; เอฟเฟกต์อยู่ที่แท่น ไม่ติดตาม Player ที่เดินออกไป
- Pause ใช้ scaled time ทั้งตัวคุมและ Particle พอปิดเอฟเฟกต์กลางทางจะลบโดยไม่ replay; โค้ดนี้เป็น one-shot ไม่ใช่ pool
- `Tools > World > Summoning > Run Magic Circle Playtest` ผ่าน 22 ข้อในฉากจริง; `Validate Magic Circle` ผ่าน 9 ข้อ ภาพ `Logs/SummoningCircle_Preview.png` และ `SummoningCircle_Gameplay.png`; วิธีปรับละเอียดอยู่ `World/SummoningSanctum/README.md` ตัวติดตั้งป้องกันการรันทับของเดิม

## 10. แผนผังไฟล์หลัก

```text
Assets/_Project/
├── Input/PlayerControls.inputactions
├── Scenes/
│   ├── PrototypeArena.unity
│   └── WorldPrototype.unity
├── Prefabs/
│   ├── Player/       HumanMalePlayerVisual.prefab (ปัจจุบัน), SpaceSoldierPlayerVisual.prefab (เดิม)
│   ├── Enemies/
│   ├── Projectiles/
│   ├── UI/           InventoryHotbar.prefab, InventoryPanel.prefab
│   └── Weapons/      ข้อมูลกระบอง โมเดลถือ และ pickup prefab
└── Scripts/
    ├── Combat/       targeting contracts, melee, ranged, projectile, highlight
    ├── Core/         camera follow, dash FOV และ GamePauseController
    ├── Enemies/      health, AI, attack, death, animation, wave spawner
    ├── Interaction/  interactable contract และ player scanner
    ├── Player/       movement, energy, health, targeting, attack, animation, visual, ItemDefinition, PlayerInventory, PlayerInputGate
    └── UI/           health, energy, prompt, hotbar, inventory panel/slot และ game over
```

เมื่อต้องเปลี่ยนค่าการเล่น ให้ตรวจทั้งค่า default ใน C# และค่าที่ serialize อยู่ใน Scene/Prefab เสมอ เพราะ Unity ใช้ค่าจาก Inspector ทับค่า default ใน source code
