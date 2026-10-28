# Git และ Git LFS สำหรับโปรเจกต์นี้

## บันทึก EverwoodTheSummonedHero และการเตรียม build

รันจากโฟลเดอร์โปรเจกต์ปัจจุบัน `TopDownShooter3D` บน branch `main`:

```sh
git add -A
git commit -m "chore: rename game to EverwoodTheSummonedHero and prepare desktop builds"
git push origin main
```

คำสั่งนี้รวมการลบ assets ที่ไม่ได้ใช้ตามงาน cleanup ด้วย ส่วนไฟล์เกมใน `Builds/` และไฟล์สำรองใน `Logs/` ถูก ignore จึงไม่ถูกอัปโหลดไปกับ source code

## ตั้งค่าแล้วบนเครื่องปัจจุบัน

- ติดตั้ง Git LFS ผ่าน Homebrew และเปิดใช้ด้วย `git lfs install --local`
- `.gitattributes` กำหนดให้โมเดล, Texture/รูป, เสียง และวิดีโอใช้ LFS อัตโนมัติตามนามสกุล รวมตัวพิมพ์ใหญ่/เล็ก
- โค้ด, Scene, Prefab, Unity YAML และ `.meta` ยังใช้ Git ปกติ เพื่ออ่าน diff ได้
- ไม่แก้ประวัติ commit เดิม ไม่ force push และไม่อัปโหลดไฟล์ระหว่างการตั้งค่า
- ไฟล์ใน Assets ยังเป็นไฟล์เต็มตามปกติ Unity เปิดใช้งานได้เหมือนเดิม

## Commit ครั้งแรกหลังเปิดใช้ LFS

บันทึก Scene และไฟล์ใน Unity ก่อน แล้วเปิด Terminal ที่โฟลเดอร์ `TopDownShooter3D`:

```sh
git add --renormalize .
git add -A
git status
git lfs status
git diff --cached --stat
```

`--renormalize` ทำให้ไฟล์ที่ Git เคยติดตามอยู่แล้วใช้กฎ LFS ใหม่ด้วย โดยไม่แก้ commit เก่า ส่วน `git add -A` รวมไฟล์ใหม่ การแก้ไข และการลบทั้งหมด จึงต้องตรวจรายการก่อน commit โดยเฉพาะไฟล์ที่แสดงว่า deleted

เมื่อตรวจรายการเรียบร้อยแล้ว:

```sh
git commit -m "feat: update gameplay and world prototype; configure Git LFS"
git push origin main
```

การ push จะส่ง LFS objects ผ่าน pre-push hook อัตโนมัติ ต้องมีสิทธิ์เข้า repository และโควตา LFS เพียงพอ การตั้งค่าในเครื่องไม่ได้ยืนยันว่าอัปโหลดสำเร็จแล้ว

ถ้า push ถูกปฏิเสธเพราะ remote มี commit ใหม่ อย่า force push ให้ตรวจและรวมการเปลี่ยนแปลงก่อน

## การใช้งานครั้งต่อไป

เพิ่ม assets ตามปกติ แล้วใช้ `git add -A`, ตรวจรายการ, `git commit` และ `git push` ได้ ไม่ต้องใช้ `--renormalize` ทุกครั้ง ยกเว้นมีการเปลี่ยนกฎใน `.gitattributes`

เก็บ `.gitattributes` และ `.meta` ใน commit เสมอ ส่วน Library, Temp, Logs และ backup ของแพ็กที่ตั้งใจเก็บนอก source control ถูกละไว้ด้วย `.gitignore`

## เปลี่ยนเครื่องหรือให้คนอื่น clone

ต้องติดตั้ง Git LFS บนเครื่องนั้นก่อนใช้งาน repository:

```sh
brew install git-lfs
git lfs install
git clone <repository-url>
```

ถ้า clone ไปก่อนติดตั้งแล้ว ให้รัน `git lfs install --local` และ `git lfs pull` ภายใน repository เพื่อดาวน์โหลดไฟล์เต็ม

อ้างอิง: [Git LFS](https://git-lfs.com/) และ [การตั้งค่า LFS บน GitHub](https://docs.github.com/en/repositories/working-with-files/managing-large-files/configuring-git-large-file-storage)
