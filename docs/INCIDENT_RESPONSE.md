# CINEMAHUB - QUY TRÌNH ỨNG PHÓ SỰ CỐ AN TOÀN THÔNG TIN (INCIDENT RESPONSE PLAN)
*Tuân thủ tiêu chuẩn NIST SP 800-61 Rev. 2 & Nghị định số 13/2023/NĐ-CP của Chính phủ Việt Nam*  
*Áp dụng cho: Hệ thống Quản lý Rạp Chiếu Phim CinemaHub (cinemahubvn.io.vn)*  
*Phiên bản: 2.0 (Cập nhật tháng 11/2026)*

---

## 1. MỤC TIÊU VÀ PHẠM VI

Quy trình này thiết lập các bước tiêu chuẩn để phát hiện, phân loại, ngăn chặn, khắc phục và báo cáo các sự cố an toàn thông tin (ATTT) và rò rỉ dữ liệu cá nhân trên toàn bộ hệ thống CinemaHub (Web Application, Database, Reverse Proxy, VPS và AI Chatbot).

### Phân cấp mức độ sự cố:
- **Mức 1 - Critical (Khẩn cấp):** Dữ liệu cá nhân bị rò rỉ ra ngoài (PII Breach), lộ khoá bí mật (Secret Leak), hệ thống bị xâm nhập chiếm quyền Root/Admin, hoặc tê liệt toàn bộ dịch vụ đặt vé.
- **Mức 2 - High (Nghiêm trọng):** Tấn công DDoS làm suy giảm hiệu năng, brute-force tài khoản quản trị quy mô lớn, phát hiện mã độc hoặc file bất thường trên server.
- **Mức 3 - Medium (Trung bình):** Tấn công dò quét lỗ hổng (Vulnerability Scanning), brute force SSH bị Fail2ban chặn, lỗi 500 phát sinh liên tục trong module thanh toán.
- **Mức 4 - Low (Thấp):** Các cảnh báo rate limit thông thường, spam bình luận, người dùng quên mật khẩu hàng loạt.

---

## 2. QUY TRÌNH 5 BƯỚC ỨNG PHÓ SỰ CỐ TIÊU CHUẨN (NIST SP 800-61)

```
[ BƯỚC 1: TIẾP NHẬN & XÁC MINH ]
       │ Cảnh báo từ Telegram Alert, Fail2ban, Log Nginx/Postgres, hoặc báo cáo người dùng
       ▼
[ BƯỚC 2: NGĂN CHẶN & CÔ LẬP (CONTAINMENT) ]
       │ Cách ly tạm thời: Cấm IP trên Fail2ban/UFW, thu hồi Session, đổi Secret bị lộ
       ▼
[ BƯỚC 3: XỬ LÝ & TIÊU DIỆT (ERADICATION) ]
       │ Vá lỗ hổng, xoá bỏ backdoor/session độc hại, khôi phục từ bản sao lưu sạch
       ▼
[ BƯỚC 4: KHÔI PHỤC HOẠT ĐỘNG (RECOVERY) ]
       │ Tái khởi động dịch vụ, kiểm tra tính toàn vẹn (Healthcheck), giám sát nâng cao
       ▼
[ BƯỚC 5: HẬU KIỂM & BÁO CÁO PHÁP LÝ (POST-INCIDENT & NĐ 13) ]
       │ Lập báo cáo phân tích nguyên nhân gốc rễ (RCA)
       │ Báo cáo Cục A05 / Bộ Công an trong vòng 72 giờ nếu có lộ lọt PII
```

---

## 3. CÁC KỊCH BẢN ỨNG PHÓ CHI TIẾT (PLAYBOOKS)

### PLAYBOOK 1: LỘ LỌT KHOÁ BÍ MẬT (SECRET / CREDENTIAL LEAK)
**Nguy cơ:** Secret (Connection String, Gemini API Key, Mail SMTP, Bot Token, PII Encryption Key) bị lộ trên Git công khai hoặc log.

1. **Ngăn chặn tức thì (< 15 phút):**
   - Đăng nhập bảng điều khiển nhà cung cấp (Google Cloud Console, Telegram BotFather, Nhà cung cấp SMTP, Cổng thanh toán VNPay).
   - **Revoke (Thu hồi)** ngay lập tức khoá cũ.
   - Sinh khoá mới với quyền hạn tối thiểu (Principle of Least Privilege).
2. **Cập nhật trên VPS:**
   - Kết nối SSH tới VPS: `ssh -i ~/.ssh/id_ed25519 root@163.223.211.158`
   - Cập nhật biến môi trường an toàn trong `/opt/cinemahub/.env.prod`.
   - Khởi động lại container bị ảnh hưởng:
     ```bash
     cd /opt/cinemahub
     docker compose --env-file .env.prod -f docker-compose.prod.yml up -d --force-recreate cinemahub_web
     ```
3. **Thanh lọc lịch sử Git:**
   - Sử dụng công cụ `git-filter-repo` hoặc BFG Repo-Cleaner để xoá triệt để commit chứa secret.
   - Kiểm tra lại bằng workflow CI/CD Gitleaks.

---

### PLAYBOOK 2: TẤN CÔNG BRUTE FORCE / TỪ CHỐI DỊCH VỤ (DDOS)
**Nguy cơ:** Kẻ tấn công cố tình đoán mật khẩu SSH port 22 hoặc gửi hàng triệu request làm sập web.

1. **Kiểm tra trạng thái phòng thủ:**
   - Kiểm tra Fail2ban:
     ```bash
     fail2ban-client status sshd
     ```
   - Kiểm tra danh sách IP đang bị cấm:
     ```bash
     fail2ban-client get sshd banned
     ```
2. **Can thiệp thủ công nếu cần thiết:**
   - Cấm thủ công IP nghi vấn bằng iptables:
     ```bash
     iptables -I INPUT -s <IP_ATTACKER> -j DROP
     ```
   - Siết chặt Rate Limiting trên Nginx `/opt/cinemahub/nginx/nginx.conf`:
     ```nginx
     limit_req zone=req_limit_per_ip burst=10 nodelay;
     ```
   - Reload Nginx: `docker exec cinemahub_nginx nginx -s reload`

---

### PLAYBOOK 3: SỰ CỐ RÒ RỈ DỮ LIỆU CÁ NHÂN (PII BREACH - NGHỊ ĐỊNH 13/2023/NĐ-CP)
**Nguy cơ:** Cơ sở dữ liệu bị truy cập trái phép hoặc dump dữ liệu ra ngoài.

1. **Đánh giá mức độ thiệt hại (< 2 giờ):**
   - Kiểm tra trường dữ liệu bị ảnh hưởng: Do CinemaHub áp dụng mã hoá AES-256-GCM, nếu kẻ tấn công chỉ lấy được file SQL thô thì toàn bộ `Email` và `FullName` đều ở dạng bản mã vô nghĩa (`PIIENC:...`).
   - Kiểm tra `AuditLogs` để xác định tài khoản hoặc endpoint nào đã bị khai thác để trích xuất dữ liệu.
2. **Quy trình báo cáo bắt buộc theo Điều 26 Nghị định 13/2023/NĐ-CP:**
   - **Thời hạn thông báo:** Không quá **72 giờ** kể từ thời điểm phát hiện sự cố.
   - **Cơ quan tiếp nhận:** Cục An ninh mạng và phòng, chống tội phạm sử dụng công nghệ cao (A05) - Bộ Công an.
   - **Hồ sơ thông báo gồm:**
     - Mô tả bản chất sự cố vi phạm quy định bảo vệ dữ liệu cá nhân.
     - Đầu mối liên hệ của Cán bộ Bảo vệ Dữ liệu (DPO).
     - Hậu quả, thiệt hại tiềm tàng đối với chủ thể dữ liệu.
     - Các biện pháp kỹ thuật đã và đang áp dụng để giảm thiểu thiệt hại (đã mã hoá AES-256-GCM, đã thu hồi quyền truy cập, đã xoá phiên làm việc).
   - **Thông báo cho khách hàng:** Gửi email cảnh báo đến các chủ thể dữ liệu bị ảnh hưởng kèm khuyến nghị đổi mật khẩu nếu phát hiện nguy cơ ảnh hưởng trực tiếp đến quyền lợi người dùng.

---

### PLAYBOOK 4: TẤN CÔNG PROMPT INJECTION VÀ LẠM DỤNG AI CHATBOT
**Nguy cơ:** Kẻ xấu cố gắng khai thác LLM để ép chatbot cung cấp vé miễn phí, chửi bới hoặc tiết lộ thông tin nội bộ.

1. **Phân tích log Chatbot:**
   - Truy xuất log tin nhắn từ bảng hoặc console log của `cinemahub_web`:
     ```bash
     docker logs --tail 200 cinemahub_web | grep "ChatBot"
     ```
2. **Xử lý:**
   - Cơ chế Prompt Guard trong `ChatBotController` đã đóng gói ngữ cảnh trong `<context_data>` và vô hiệu hoá lệnh điều khiển từ dữ liệu.
   - Phản hồi từ LLM được lọc thẻ `<script>` và kiểm soát độ dài tự động.
   - Nếu một IP liên tục spam tấn công LLM, cơ chế ASP.NET Core RateLimiter (`ChatBotLimit`) và Nginx zone `api_limit` sẽ tự động chặn HTTP 429.

---

## 4. THÔNG TIN LIÊN HỆ ĐỘI ỨNG CỨU SỰ CỐ (CSIRT)

- **Trưởng ban Ứng phó Sự cố (Incident Commander):** Quản trị viên Hệ thống CinemaHub
- **Hotline Khẩn cấp (24/7):** 0344 596 643
- **Email tiếp nhận sự cố:** support@cinemahubvn.io.vn / ducmanh2005vt@gmail.com
- **Địa chỉ:** 235 Hoàng Quốc Việt, Cổ Nhuế, Bắc Từ Liêm, Hà Nội
- **Kênh cảnh báo tự động:** Telegram Security Channel (`@CinemaHub_Security_Bot`)

