# Everwood — ความพร้อมสำหรับต่อเนื้อเรื่อง

ตรวจจาก `Story.pdf` (Everwood: The Summoned Hero, 8 หน้า) เทียบกับโค้ดและฉาก `Assets/_Project/Scenes/WorldPrototype.unity` วันที่ 28 ตุลาคม 2026

**อัปเดต:** สามส่วนแรกติดตั้งแล้ว: quest/dialogue/save, Tutorial → Explore Everwood, และ Guard → Defend the Village → Hunt the Alpha Wolf พร้อมส่งรางวัล อ่านรายละเอียดปัจจุบันที่ [STORY_QUESTS_AND_DIALOGUE.md](STORY_QUESTS_AND_DIALOGUE.md) ตารางส่วนที่ยังไม่ติดตั้งด้านล่างเป็นแผนสำหรับ Story ช่วงถัดไป

## ข้อตกลงที่ยืนยันแล้ว

- คง Potion แดง/ฟ้า/เขียว 3 ขวดปัจจุบัน แม้เอกสารใช้ชื่อ Health Potion / Greater Potion
- KingOfDemon เป็น **Malachar** บอสสุดท้าย ขนาดเดิมที่ตั้งไว้ 3 เท่าของผู้เล่น; DemonLord เป็นรองบอสตามเดิม
- เมืองล่างขวา = **Silverwell Town**, หมู่บ้านซ้ายบน = **Oakshade Village**, ป่าระหว่างทาง = **Thornveil Forest**, ปราสาทขวาบน = **Malachar's Castle**, จุดเริ่มซ้ายล่าง = **Summoning Ruins**
- ตอนนี้เล่นฉาก 01–03 พร้อมรางวัลและเซฟได้แล้ว เนื้อเรื่องส่วนร้านค้า น้ำพุ และ Malachar/Victory ยังอยู่ในงานช่วงถัดไป

## Asset ที่ควรหาเพิ่ม

| ลำดับ | สิ่งที่ต้องหา | ใช้ทำอะไร | สิ่งที่ต้องมีในแพ็ก |
| --- | --- | --- | --- |
| 1 | **Wood Golem / โกเลมไม้** | มอนสเตอร์พิเศษใน Thornveil Forest และ Golem Core | โมเดลมี rig, Idle, Walk, Attack, Death; ท่าโจมตีหนักหรือเสียงไม้ช่วยเพิ่มบุคลิกได้ |
| ตามต้องการ | **จอมเวทชราตัวจริง** | เปลี่ยนภาพลักษณ์จาก NPC จำลองในภายหลัง | ตอนนี้ใช้ HumanMale ผม/เคราขาวพร้อมไม้เท้าเป็นตัวจำลองใกล้จุดอัญเชิญ ทำระบบต่อได้ก่อน |

Alpha Wolf ใช้ **01_Monster: Wolf Boss ของ HATOGAME** แล้ว มีท่า Idle, Walk, Run, Attack และ Death ของ rig เดียวกัน ไม่ใช้ท่ามังกรหรือโมเดล alphaWolf เดิมแล้ว ยังขาด Wood Golem; ควรเลือกสไตล์ที่เข้ากับฉาก พร้อม material ที่รองรับ URP หรือ texture สำหรับแปลง ใช้ Generic หรือ Humanoid ได้ตามชุด animation ที่แถมมา

ไม่จำเป็นต้องโหลดแพ็กใหม่เพื่อทำร้านขายยา ช่างตีเหล็ก เจ้าหน้าที่กิลด์ ทหารยาม จุดอัญเชิญ หรือบอสสุดท้าย มีของเดิมใช้ต่อได้แล้ว น้ำพุกลางเมืองมีโมเดลอยู่แล้วเช่นกัน

Wolf Fang มี ItemDefinition แล้ว เป็นรางวัลเควส Alpha Wolf และขายให้กิลด์ได้ 60 Gold ใช้ icon เขี้ยวเดิม ส่วน Golem Core และ drop table ของ Wood Golem ยังต้องทำภายหลัง

## NPC ที่เตรียมไว้ในฉาก

| บทบาทใน Story | ตัวที่ใช้ | ทำแล้ว | ยังต้องต่อ |
| --- | --- | --- | --- |
| Village Guard | `NPC_Oakshade_Guard` ใกล้ทางเข้าหมู่บ้าน | HumanMale + เกราะ, Idle/หันมองผู้เล่น, กด E คุย, รับ/ส่งเควส defense และ wolf พร้อมบทพูดตามสถานะแล้ว | พร้อมปรับแต่งภาพลักษณ์/บทพูดต่อ |
| Elder Wizard | `NPC_ElderWizard_Placeholder` ใกล้จุดอัญเชิญ | NPC เดิมผม/เคราขาว ชุดสีม่วงเข้ม ไม้เท้าชั่วคราว มี Idle และหันมองผู้เล่น | บทสนทนา/เหตุการณ์ฉากเปิด และเปลี่ยนโมเดลภายหลัง |
| Alchemist | NPC ผู้หญิงหน้า Potion Store เดิม | ร้าน Potion 3 ชนิดและระบบ Gold เดิม; เพิ่ม StoryActor ID | บทพูดครั้งแรก/กลับมาซื้อซ้ำและข้อความตาม Story |
| Blacksmith | `NPC_WeaponMerchant` เดิม | ร้านดาบ, stock/ราคาหมุนเวียนเดิม; เพิ่ม StoryActor ID | บทสนทนาแบบ Story และขั้นยืนยันซื้อให้ตรงสคริปต์ |
| Guild Receptionist | NPC ผู้หญิงหน้า Gild เดิม | รับซื้อวัตถุดิบแลก Gold; เพิ่ม StoryActor ID | บทสนทนาครั้งแรก/กลับมาใหม่ และขั้นยืนยันขายตาม Story |
| Malachar | `KingOfDemon` เดิม | คง AI, arena, animation, VFX และเลือดบอส; เปลี่ยนชื่อที่ HUD แสดงเป็น MALACHAR | บทพูดก่อนสู้, เฟสสอง, เหตุการณ์ตาย/จบเกม |

ตัว NPC ร้านค้าเดิมยังอยู่ใต้อาคารเดิม จึงย้ายตามอาคารได้ตามที่จัดไว้ `StoryActor` เก็บตัวตน/บทบาท ส่วน `PlayerStoryProgress` เก็บความคืบหน้าและการเคยคุยกับทหารยามแล้ว บทพูด first/repeat ของร้านค้าเป็นงานถัดไป

ทหารยามเป็น NPC ประจำจุดแบบไม่ต่อสู้ในรอบนี้ ไม่ถูกนับเพิ่มในประชากรชาวบ้านและไม่เกิดใหม่ตามระบบ TownNpcPopulation ชุดใช้เกราะเริ่มต้นจาก Blink HumanMale ที่มีอยู่ ไม่ใช่ชุดอัศวินเหล็กหนัก

## เทียบเนื้อเรื่องทีละส่วน

| ส่วนใน Story | มีพื้นฐานแล้ว | งานที่ยังขาด |
| --- | --- | --- |
| Opening — The Fall of Everwood | ฉาก/VFX/มอนสเตอร์บางส่วนใช้เป็นแหล่งภาพได้ | วิดีโอเปิด, ภาพหรือ storyboard, เสียง/คำบรรยาย, VideoPlayer หรือระบบเล่น cinematic และข้ามฉาก |
| 01 Summoning Ruins | ติดตั้ง Main Quest: Defeat Malachar, ตรวจเดิน/ยิง/dash จริง, เป้าฝึก และ Explore Everwood พร้อมเซฟแล้ว | ไม่มี cutscene เพิ่ม; วิดีโอเปิดทำภายหลัง |
| 02 Defend the Village | รับเควสจากทหารยาม, ผู้บุกรุก 3×2, เซฟ kill mask, กลับมาส่งรับ 100 Gold + หนังสัตว์ 2 ชิ้นแล้ว | พร้อมเล่นและปรับสมดุลต่อจาก asset เควส/raid prefab |
| 03 Hunt the Alpha Wolf | ต่อจาก defense, ต้องฆ่า Wolf Boss ด้วยผู้เล่นหลังรับเควส, ส่งรับ 180 Gold + Wolf Fang 1 ชิ้นแล้ว | พร้อมเล่น; Wolf Fang ขายที่กิลด์ได้ 60 Gold |
| 04 The Alchemist | ร้านยา 3 สี ซื้อและใช้ได้จริง | บทพูดตามสถานการณ์, first/repeat visit; คง 3 ขวดตามที่ยืนยัน |
| 05 The Blacksmith | ร้านดาบเท่านั้น, ราคา/stock, เลือกช่องอาวุธ | บทพูดและขั้นยืนยัน/ยกเลิกให้ตรง Story; ไม่เพิ่มร้านปืน |
| 06 Adventurer's Guild | ขายวัตถุดิบแลก Gold, แสดงของในกระเป๋า | first/repeat dialogue, confirm/cancel sale ตามสคริปต์, เพิ่มรายการ Wolf Fang/Golem Core |
| 07 Magic Fountain | โมเดลน้ำพุกลางเมือง | กด E, ฟื้น HP, ตั้งและบันทึก checkpoint, ตายแล้วกลับจุดนั้น, ข้อความตอบรับ |
| 08 Thornveil Forest | encounter/respawn/loot, ดูดของเข้ากระเป๋า, Alpha Wolf หนึ่งตัวในประชากรป่า | Wood Golem, drop เฉพาะ และข้อความสำรวจที่จำเป็น |
| 09 Malachar's Castle | พื้นที่และ boss arena, AI/HP HUD/animation/VFX | บทพูดก่อนสู้และล็อกเริ่ม combat จนจบบทพูด; ปัจจุบันเข้า arena แล้ว AI ทำงานตามระบบเดิม |
| 10 Boss Phase 2 | สกิลและ VFX บอสใช้เป็นฐานได้ | ตรวจ HP ≤ 50% ครั้งเดียว, บทพูด, enrage, ปรับความถี่/ความแรง และ VFX |
| 11 Demon King Falls | บอสมีท่าตายและเลือดหมดได้ | death dialogue, Final Boss Defeated, Main Quest Complete, รอท่าตายก่อนเปิด Victory |
| 12 Victory | มีระบบ UI พื้นฐาน | หน้าจอ Victory ตามข้อความใน Story และสถานะจบเกม |

ระบบ save ปัจจุบันบันทึก progression, Gold/อาวุธ/กระเป๋า และ Story แยก key มี reward receipt ใน wallet ป้องกันรับรางวัลเควสซ้ำแล้ว ยังไม่มีตำแหน่ง checkpoint และ NPC Hotel ยังไม่ได้ทำเมนู save การเพิ่ม checkpoint น้ำพุต้องเชื่อมกับการตาย/เริ่มเกมใหม่ในรอบถัดไป

## Mini-map ที่เพิ่ม

- อยู่ขวาบนแทนเข็มทิศหลัก แสดงภาพรวมอาณาจักรแบบทิศเหนืออยู่ด้านบน
- ลูกศรผู้เล่นขยับตามตำแหน่ง X/Z และหมุนตามทิศตัวละคร; ออกจากขอบภาพแล้วลูกศรยังอยู่ในกรอบ
- ชื่อ Oakshade Village, Silverwell Town, Malachar's Castle และ Summoning Ruins อิง Transform ของพื้นที่จริง มีชื่อ Thornveil Forest กลางแผนที่
- รองรับทั้งกล้อง top-down และ third person เพราะตำแหน่งอ้างอิงโลก ไม่ขึ้นกับการหมุนกล้อง
- ใช้ภาพที่สร้างใน Editor จึงไม่มีกล้อง mini-map ที่ต้อง render โลกซ้ำทุกเฟรม
- การเปิดกระเป๋าและร้านค้ายังคงอยู่เหนือ mini-map
- เพิ่ม marker เป้าหมายเควสสีทองแล้ว ยังไม่มี fog of war, จุดมอนสเตอร์ทั่วไป, zoom หรือแผนที่เต็มจอ

**เมื่อย้ายอาคาร ปรับ terrain หรือเปลี่ยนฉาก:** เปิด WorldPrototype ใน Edit Mode แล้วเลือก **Tools → Story → Rebuild Minimap Overview After Map Edits** เพื่อสร้างภาพพื้นหลังและขอบเขตใหม่ Marker อ้างอิงตำแหน่งจริง แต่ภาพพื้นหลังไม่ได้วาดใหม่อัตโนมัติระหว่างเล่น

## จุดสำคัญสำหรับเขียนระบบต่อ

| ไฟล์/ตำแหน่งในโปรเจกต์ | หน้าที่ |
| --- | --- |
| `Assets/_Project/Scripts/Core/StoryActor.cs` | ตัวตนของ NPC/บอสสำหรับจับคู่ข้อมูล Story; ใช้ ActorId ที่คงเดิม |
| `Assets/_Project/Scripts/Core/WorldMapLandmark.cs` | ชื่อและจุดพื้นที่ในฉาก |
| `Assets/_Project/Scripts/UI/KingdomMinimapUI.cs` | แปลงพิกัดโลกและอัปเดตลูกศร/ชื่อสถานที่ |
| `Assets/_Project/Story/NPCs/NPC_Oakshade_Guard.prefab` | โมเดลและพฤติกรรมพื้นฐานของทหารยาม |
| `Assets/_Project/Story/Materials/` | Material ของทหารยามแยกจากผู้เล่น |
| `Assets/_Project/Story/Map/Everwood_Overview.png` | ภาพพื้นหลัง mini-map |
| `Assets/_Project/Editor/StoryWorld*.cs`, `StoryMinimapBuild.cs` | ตัวช่วยเตรียมฉากและสร้างภาพแผนที่ |
| `Assets/_Project/Scripts/Enemies/KingOfDemonAI.cs` | จุดต่อ dialogue gate, enrage และเหตุการณ์หลังตายของ Malachar |
| `Assets/_Project/Scripts/Core/GamePauseController.cs` | ให้บทสนทนาหยุด gameplay ด้วยเหตุผล Dialogue โดยไม่รบกวน pause ของหน้าต่างอื่น |

ActorId ที่ใช้: `oakshade_guard`, `silverwell_alchemist`, `silverwell_blacksmith`, `silverwell_guild_receptionist`, `malachar`

แนะนำให้เก็บ Quest ID, ขั้นตอนปัจจุบัน, objective counters, reward claimed, NPC first visit, checkpoint และ victory ใน save เดียวที่มี version และผูกกับเหตุการณ์จากระบบเดิม หลีกเลี่ยงให้ NPC เช็กชื่อ GameObject หรือชื่อคลิป animation เพื่ออนุมานว่าเควสสำเร็จ เพราะการย้าย/เปลี่ยนชื่อฉากภายหลังจะทำให้เควสพังได้

## ลำดับงานรอบต่อไป

1. **เสร็จแล้ว** — Quest state + Dialogue + save ของเรื่อง และ tutorial ที่ Summoning Ruins
2. **เสร็จแล้ว** — Guard dialogue → Defend the Village → ส่งเควส/รางวัล
3. **เสร็จแล้วสำหรับ Alpha Wolf** — Hunt the Alpha Wolf + Wolf Fang; Wood Golem + Golem Core ทำเมื่อได้ asset
4. ต่อ first/repeat dialogue ของร้านเดิม และน้ำพุฟื้น HP/ตั้ง checkpoint
5. Malachar dialogue gate → เฟสสอง 50% → death event → Victory UI
6. ใส่วิดีโอเปิดเมื่อรูปแบบและไฟล์สื่อพร้อม

สามส่วนแรกพร้อมทดสอบใน WorldPrototype แล้ว งานส่วนร้านค้า/น้ำพุ/บอสต่อได้ด้วย asset เดิม โดย Wood Golem ยังต้องมี asset เพิ่ม

## การตรวจงานรอบนี้

ใช้เมนู **Tools → Story → Run NPC and Minimap Play Test** ทดสอบ rig/material/Idle ของทหารยาม, จุดยืนบน NavMesh, ตัวตนของร้านเดิมและ Malachar, พิกัด/ทิศ/ขอบ mini-map, ชื่อพื้นที่, ลำดับกระเป๋าเหนือแผนที่ และตรวจว่าไม่เปลี่ยน Gold/ไอเทมระหว่างทดสอบ รายงานอยู่ใน `Logs/StoryPresentationPlaytest.txt` และภาพ `Logs/StoryMinimap_Play.png`, `Logs/StoryGuard_Play.png`

ผลล่าสุด: **ผ่าน 32 ข้อใน Unity Play Mode** ไม่มี runtime error หรือคำเตือน glyph หายระหว่างการตรวจ ไม่มีการทดสอบเควสหรือ Victory เพราะยังไม่ได้ implement ในรอบนี้

การเปลี่ยน Wolf Boss / Giant Spider / จอมเวทจำลอง และการลบ asset เก่า ดูเพิ่มเติมที่ [NATIVE_CREATURES_AND_ASSET_CLEANUP.md](NATIVE_CREATURES_AND_ASSET_CLEANUP.md)
