-- Dữ liệu mẫu danh mục phòng khám (Development). Spec: docs/specs/CLINIC-CATALOG-SQL-SEED.md
-- Sinh tự động từ các seeder C# (--seed-clinic-catalog). Idempotent, một giao dịch, không phụ thuộc ID.
-- Chạy bằng psql với -v ON_ERROR_STOP=1 (import-clinic-catalog.ps1 đã làm sẵn).
-- Điều kiện: đã chạy start-api.ps1 -Migrate -SeedDemo (chi nhánh "S09 - Chi nhánh thử nghiệm" và tài khoản dentist.demo).
BEGIN;
SELECT pg_advisory_xact_lock(hashtext('dcms-clinic-catalog-seed'));
CREATE TEMP TABLE seed_ctx ON COMMIT DROP AS
SELECT (SELECT "Id" FROM "SCHEDULING_BRANCH" WHERE "Name" = 'S09 - Chi nhánh thử nghiệm' LIMIT 1) AS branch_id,
       (SELECT "Id" FROM "USER_ACCOUNT" WHERE "UserName" = 'dentist.demo' LIMIT 1) AS actor_id;
DO $$ BEGIN
  IF (SELECT branch_id FROM seed_ctx) IS NULL THEN RAISE EXCEPTION 'Chưa có chi nhánh demo: chạy start-api.ps1 -Migrate -SeedDemo trước.'; END IF;
  IF (SELECT actor_id FROM seed_ctx) IS NULL THEN RAISE EXCEPTION 'Chưa có tài khoản dentist.demo: chạy start-api.ps1 -Migrate -SeedDemo trước.'; END IF;
END $$;

-- Nhóm vật liệu
INSERT INTO "MATERIAL_CATEGORY" ("Id","Name","NormalizedName","Version","IsDeleted","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),'Chỉ khâu và phẫu thuật','CHỈ KHÂU VÀ PHẪU THUẬT',1,FALSE,now(),now()
WHERE NOT EXISTS (SELECT 1 FROM "MATERIAL_CATEGORY" WHERE "NormalizedName"='CHỈ KHÂU VÀ PHẪU THUẬT' AND NOT "IsDeleted");
INSERT INTO "MATERIAL_CATEGORY" ("Id","Name","NormalizedName","Version","IsDeleted","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),'Dụng cụ cầm tay','DỤNG CỤ CẦM TAY',1,FALSE,now(),now()
WHERE NOT EXISTS (SELECT 1 FROM "MATERIAL_CATEGORY" WHERE "NormalizedName"='DỤNG CỤ CẦM TAY' AND NOT "IsDeleted");
INSERT INTO "MATERIAL_CATEGORY" ("Id","Name","NormalizedName","Version","IsDeleted","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),'Dụng cụ nội nha và mũi khoan','DỤNG CỤ NỘI NHA VÀ MŨI KHOAN',1,FALSE,now(),now()
WHERE NOT EXISTS (SELECT 1 FROM "MATERIAL_CATEGORY" WHERE "NormalizedName"='DỤNG CỤ NỘI NHA VÀ MŨI KHOAN' AND NOT "IsDeleted");
INSERT INTO "MATERIAL_CATEGORY" ("Id","Name","NormalizedName","Version","IsDeleted","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),'Implant','IMPLANT',1,FALSE,now(),now()
WHERE NOT EXISTS (SELECT 1 FROM "MATERIAL_CATEGORY" WHERE "NormalizedName"='IMPLANT' AND NOT "IsDeleted");
INSERT INTO "MATERIAL_CATEGORY" ("Id","Name","NormalizedName","Version","IsDeleted","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),'Lấy dấu và phục hình','LẤY DẤU VÀ PHỤC HÌNH',1,FALSE,now(),now()
WHERE NOT EXISTS (SELECT 1 FROM "MATERIAL_CATEGORY" WHERE "NormalizedName"='LẤY DẤU VÀ PHỤC HÌNH' AND NOT "IsDeleted");
INSERT INTO "MATERIAL_CATEGORY" ("Id","Name","NormalizedName","Version","IsDeleted","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),'Thuốc tê và kim','THUỐC TÊ VÀ KIM',1,FALSE,now(),now()
WHERE NOT EXISTS (SELECT 1 FROM "MATERIAL_CATEGORY" WHERE "NormalizedName"='THUỐC TÊ VÀ KIM' AND NOT "IsDeleted");
INSERT INTO "MATERIAL_CATEGORY" ("Id","Name","NormalizedName","Version","IsDeleted","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),'Tẩy trắng','TẨY TRẮNG',1,FALSE,now(),now()
WHERE NOT EXISTS (SELECT 1 FROM "MATERIAL_CATEGORY" WHERE "NormalizedName"='TẨY TRẮNG' AND NOT "IsDeleted");
INSERT INTO "MATERIAL_CATEGORY" ("Id","Name","NormalizedName","Version","IsDeleted","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),'Vô trùng và bảo hộ','VÔ TRÙNG VÀ BẢO HỘ',1,FALSE,now(),now()
WHERE NOT EXISTS (SELECT 1 FROM "MATERIAL_CATEGORY" WHERE "NormalizedName"='VÔ TRÙNG VÀ BẢO HỘ' AND NOT "IsDeleted");
INSERT INTO "MATERIAL_CATEGORY" ("Id","Name","NormalizedName","Version","IsDeleted","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),'Vật liệu nội nha','VẬT LIỆU NỘI NHA',1,FALSE,now(),now()
WHERE NOT EXISTS (SELECT 1 FROM "MATERIAL_CATEGORY" WHERE "NormalizedName"='VẬT LIỆU NỘI NHA' AND NOT "IsDeleted");
INSERT INTO "MATERIAL_CATEGORY" ("Id","Name","NormalizedName","Version","IsDeleted","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(),'Vật liệu trám','VẬT LIỆU TRÁM',1,FALSE,now(),now()
WHERE NOT EXISTS (SELECT 1 FROM "MATERIAL_CATEGORY" WHERE "NormalizedName"='VẬT LIỆU TRÁM' AND NOT "IsDeleted");

-- Dụng cụ/vật liệu
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Abutment Implant','ABUTMENT IMPLANT',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Implant' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='ABUTMENT IMPLANT' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Alginate lấy dấu','ALGINATE LẤY DẤU',c."Id",1,FALSE,now(),now(),'g'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Lấy dấu và phục hình' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='ALGINATE LẤY DẤU' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Axit etching','AXIT ETCHING',c."Id",1,FALSE,now(),now(),'mL'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vật liệu trám' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='AXIT ETCHING' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Bay trám composite','BAY TRÁM COMPOSITE',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ cầm tay' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='BAY TRÁM COMPOSITE' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Bông cuộn nha khoa','BÔNG CUỘN NHA KHOA',c."Id",1,FALSE,now(),now(),'cuộn'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vô trùng và bảo hộ' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='BÔNG CUỘN NHA KHOA' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Chỉ khâu không tiêu','CHỈ KHÂU KHÔNG TIÊU',c."Id",1,FALSE,now(),now(),'sợi'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Chỉ khâu và phẫu thuật' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='CHỈ KHÂU KHÔNG TIÊU' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Chỉ khâu tiêu','CHỈ KHÂU TIÊU',c."Id",1,FALSE,now(),now(),'sợi'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Chỉ khâu và phẫu thuật' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='CHỈ KHÂU TIÊU' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Chốt sợi','CHỐT SỢI',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vật liệu nội nha' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='CHỐT SỢI' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Composite thẩm mỹ','COMPOSITE THẨM MỸ',c."Id",1,FALSE,now(),now(),'g'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vật liệu trám' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='COMPOSITE THẨM MỸ' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Composite đặc','COMPOSITE ĐẶC',c."Id",1,FALSE,now(),now(),'g'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vật liệu trám' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='COMPOSITE ĐẶC' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Cây lấy cao răng tay','CÂY LẤY CAO RĂNG TAY',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ cầm tay' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='CÂY LẤY CAO RĂNG TAY' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Cây nạy răng','CÂY NẠY RĂNG',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ cầm tay' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='CÂY NẠY RĂNG' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Côn giấy','CÔN GIẤY',c."Id",1,FALSE,now(),now(),'cây'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vật liệu nội nha' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='CÔN GIẤY' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Dung dịch NaOCl 3%','DUNG DỊCH NAOCL 3%',c."Id",1,FALSE,now(),now(),'mL'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vật liệu nội nha' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='DUNG DỊCH NAOCL 3%' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Dung dịch sát khuẩn dụng cụ','DUNG DỊCH SÁT KHUẨN DỤNG CỤ',c."Id",1,FALSE,now(),now(),'mL'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vô trùng và bảo hộ' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='DUNG DỊCH SÁT KHUẨN DỤNG CỤ' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Gel tê bề mặt','GEL TÊ BỀ MẶT',c."Id",1,FALSE,now(),now(),'g'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Thuốc tê và kim' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='GEL TÊ BỀ MẶT' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Gel tẩy trắng tại ghế','GEL TẨY TRẮNG TẠI GHẾ',c."Id",1,FALSE,now(),now(),'bộ'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Tẩy trắng' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='GEL TẨY TRẮNG TẠI GHẾ' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Gel tẩy trắng tại nhà','GEL TẨY TRẮNG TẠI NHÀ',c."Id",1,FALSE,now(),now(),'ống'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Tẩy trắng' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='GEL TẨY TRẮNG TẠI NHÀ' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Giấy cắn','GIẤY CẮN',c."Id",1,FALSE,now(),now(),'tờ'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vật liệu trám' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='GIẤY CẮN' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Glass ionomer (GIC) viên nang','GLASS IONOMER (GIC) VIÊN NANG',c."Id",1,FALSE,now(),now(),'viên'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vật liệu trám' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='GLASS IONOMER (GIC) VIÊN NANG' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Gutta-percha','GUTTA-PERCHA',c."Id",1,FALSE,now(),now(),'cây'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vật liệu nội nha' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='GUTTA-PERCHA' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Găng tay y tế','GĂNG TAY Y TẾ',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vô trùng và bảo hộ' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='GĂNG TAY Y TẾ' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Gương khám nha khoa','GƯƠNG KHÁM NHA KHOA',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ cầm tay' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='GƯƠNG KHÁM NHA KHOA' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Keo dán composite','KEO DÁN COMPOSITE',c."Id",1,FALSE,now(),now(),'mL'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vật liệu trám' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='KEO DÁN COMPOSITE' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Khăn trải bệnh nhân','KHĂN TRẢI BỆNH NHÂN',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vô trùng và bảo hộ' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='KHĂN TRẢI BỆNH NHÂN' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Khẩu trang y tế','KHẨU TRANG Y TẾ',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vô trùng và bảo hộ' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='KHẨU TRANG Y TẾ' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Kim bơm rửa ống tủy','KIM BƠM RỬA ỐNG TỦY',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ nội nha và mũi khoan' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='KIM BƠM RỬA ỐNG TỦY' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Kim tê nha khoa','KIM TÊ NHA KHOA',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Thuốc tê và kim' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='KIM TÊ NHA KHOA' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Kìm nhổ răng','KÌM NHỔ RĂNG',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ cầm tay' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='KÌM NHỔ RĂNG' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Kẹp gắp nha khoa','KẸP GẮP NHA KHOA',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ cầm tay' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='KẸP GẮP NHA KHOA' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Lưỡi dao mổ','LƯỠI DAO MỔ',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Chỉ khâu và phẫu thuật' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='LƯỠI DAO MỔ' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Mũi khoan kim cương','MŨI KHOAN KIM CƯƠNG',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ nội nha và mũi khoan' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='MŨI KHOAN KIM CƯƠNG' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Mũi khoan tay chậm','MŨI KHOAN TAY CHẬM',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ nội nha và mũi khoan' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='MŨI KHOAN TAY CHẬM' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Sealer nội nha','SEALER NỘI NHA',c."Id",1,FALSE,now(),now(),'bộ'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vật liệu nội nha' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='SEALER NỘI NHA' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Silicone lấy dấu','SILICONE LẤY DẤU',c."Id",1,FALSE,now(),now(),'bộ'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Lấy dấu và phục hình' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='SILICONE LẤY DẤU' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Thuốc tê Lidocaine 2% (ống 1,8 mL)','THUỐC TÊ LIDOCAINE 2% (ỐNG 1,8 ML)',c."Id",1,FALSE,now(),now(),'ống'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Thuốc tê và kim' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='THUỐC TÊ LIDOCAINE 2% (ỐNG 1,8 ML)' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Thám trâm','THÁM TRÂM',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ cầm tay' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='THÁM TRÂM' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Trâm máy NiTi','TRÂM MÁY NITI',c."Id",1,FALSE,now(),now(),'cây'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ nội nha và mũi khoan' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='TRÂM MÁY NITI' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Trâm nội nha tay K-file','TRÂM NỘI NHA TAY K-FILE',c."Id",1,FALSE,now(),now(),'cây'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Dụng cụ nội nha và mũi khoan' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='TRÂM NỘI NHA TAY K-FILE' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Trụ Implant','TRỤ IMPLANT',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Implant' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='TRỤ IMPLANT' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Trụ lành thương','TRỤ LÀNH THƯƠNG',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Implant' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='TRỤ LÀNH THƯƠNG' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Túi hấp tiệt trùng','TÚI HẤP TIỆT TRÙNG',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vô trùng và bảo hộ' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='TÚI HẤP TIỆT TRÙNG' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Vật liệu làm răng tạm','VẬT LIỆU LÀM RĂNG TẠM',c."Id",1,FALSE,now(),now(),'bộ'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Lấy dấu và phục hình' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='VẬT LIỆU LÀM RĂNG TẠM' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Xi măng gắn mão','XI MĂNG GẮN MÃO',c."Id",1,FALSE,now(),now(),'bộ'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Lấy dấu và phục hình' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='XI MĂNG GẮN MÃO' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Xốp cầm máu','XỐP CẦM MÁU',c."Id",1,FALSE,now(),now(),'miếng'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Chỉ khâu và phẫu thuật' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='XỐP CẦM MÁU' AND NOT x."IsDeleted");
INSERT INTO "MATERIAL_CATALOG_ITEM" ("Id","Name","NormalizedName","CategoryId","Version","IsDeleted","CreatedAt","UpdatedAt","UsageUnit")
SELECT gen_random_uuid(),'Ống hút nước bọt','ỐNG HÚT NƯỚC BỌT',c."Id",1,FALSE,now(),now(),'cái'
FROM "MATERIAL_CATEGORY" c WHERE c."Name"='Vô trùng và bảo hộ' AND NOT c."IsDeleted"
AND NOT EXISTS (SELECT 1 FROM "MATERIAL_CATALOG_ITEM" x WHERE x."CategoryId"=c."Id" AND x."NormalizedName"='ỐNG HÚT NƯỚC BỌT' AND NOT x."IsDeleted");

-- Dịch vụ của chi nhánh demo
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Chụp phim panorama',10,TRUE,FALSE,FALSE,250000,'phim','phim'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Chụp phim panorama');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Chụp phim quanh chóp',5,TRUE,FALSE,FALSE,NULL,'phim','phim'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Chụp phim quanh chóp');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Chụp CT Cone Beam',15,TRUE,FALSE,FALSE,NULL,'phim','phim'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Chụp CT Cone Beam');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Cạo vôi răng, đánh bóng',40,TRUE,FALSE,FALSE,300000,'2 hàm','2 hàm'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Cạo vôi răng, đánh bóng');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Cạo vôi răng nhiều',60,TRUE,FALSE,FALSE,700000,'2 hàm','2 hàm'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Cạo vôi răng nhiều');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Bôi fluor',15,TRUE,FALSE,FALSE,300000,'lần','lần'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Bôi fluor');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Trám bít hố rãnh',20,TRUE,FALSE,FALSE,200000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Trám bít hố rãnh');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Điều trị ê buốt răng',20,TRUE,FALSE,FALSE,NULL,'lần','lần'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Điều trị ê buốt răng');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Trám răng GIC',25,TRUE,FALSE,FALSE,200000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Trám răng GIC');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Trám răng composite',35,TRUE,FALSE,FALSE,400000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Trám răng composite');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Trám composite thẩm mỹ răng trước',50,TRUE,FALSE,FALSE,700000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Trám composite thẩm mỹ răng trước');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Hàn cổ răng',25,TRUE,FALSE,FALSE,300000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Hàn cổ răng');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Đóng khe thưa bằng composite',60,TRUE,FALSE,FALSE,1750000,'2 răng','2 răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Đóng khe thưa bằng composite');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Mở tủy giảm đau',30,TRUE,FALSE,FALSE,NULL,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Mở tủy giảm đau');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Điều trị tủy răng 1 chân',60,TRUE,FALSE,FALSE,1000000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Điều trị tủy răng 1 chân');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Điều trị tủy răng hàm nhỏ',75,TRUE,FALSE,FALSE,1550000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Điều trị tủy răng hàm nhỏ');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Điều trị tủy răng hàm lớn',90,TRUE,FALSE,FALSE,2900000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Điều trị tủy răng hàm lớn');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Chốt sợi, cùi giả',30,TRUE,FALSE,FALSE,1500000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Chốt sợi, cùi giả');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Nhổ răng đơn giản',25,TRUE,FALSE,FALSE,350000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Nhổ răng đơn giản');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Nhổ răng khôn hàm trên độ 1-2',45,TRUE,FALSE,FALSE,1450000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Nhổ răng khôn hàm trên độ 1-2');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Nhổ răng khôn hàm dưới độ 1-2',60,TRUE,FALSE,FALSE,2200000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Nhổ răng khôn hàm dưới độ 1-2');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Cắt chỉ, tái khám sau nhổ',15,TRUE,FALSE,FALSE,100000,'lần','lần'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Cắt chỉ, tái khám sau nhổ');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Rạch dẫn lưu áp xe lợi',30,TRUE,FALSE,FALSE,900000,'lần','lần'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Rạch dẫn lưu áp xe lợi');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Nạo túi nha chu (túi không quá 5 mm)',45,TRUE,FALSE,FALSE,400000,'túi','túi'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Nạo túi nha chu (túi không quá 5 mm)');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Cắt lợi trùm, tạo hình lợi nhỏ',40,TRUE,FALSE,FALSE,1250000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Cắt lợi trùm, tạo hình lợi nhỏ');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Trám răng sữa',25,TRUE,FALSE,FALSE,300000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Trám răng sữa');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Nhổ răng sữa',15,TRUE,FALSE,FALSE,100000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Nhổ răng sữa');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Lấy tủy răng sữa và trám',40,TRUE,FALSE,FALSE,750000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Lấy tủy răng sữa và trám');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Mão răng sứ kim loại',60,TRUE,FALSE,FALSE,2750000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Mão răng sứ kim loại');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Mão răng sứ Titanium',60,TRUE,FALSE,FALSE,2500000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Mão răng sứ Titanium');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Mão răng toàn sứ Zirconia',60,TRUE,FALSE,FALSE,6250000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Mão răng toàn sứ Zirconia');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Mão răng Emax',60,TRUE,FALSE,FALSE,9000000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Mão răng Emax');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Cầu răng sứ kim loại',60,TRUE,FALSE,FALSE,2750000,'đơn vị','đơn vị'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Cầu răng sứ kim loại');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Inlay/Onlay sứ',50,TRUE,FALSE,FALSE,3250000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Inlay/Onlay sứ');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Mặt dán sứ Veneer',75,TRUE,FALSE,FALSE,9000000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Mặt dán sứ Veneer');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Hàm tháo lắp nhựa',30,TRUE,FALSE,FALSE,3000000,'hàm','hàm'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Hàm tháo lắp nhựa');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Hàm tháo lắp khung kim loại',30,TRUE,FALSE,FALSE,7500000,'hàm','hàm'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Hàm tháo lắp khung kim loại');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Tháo mão, phục hình tạm',25,TRUE,FALSE,FALSE,300000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Tháo mão, phục hình tạm');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Lấy dấu, thử khung phục hình',45,TRUE,FALSE,FALSE,NULL,'lần','lần'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Lấy dấu, thử khung phục hình');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Gắn phục hình, giao hàm',30,TRUE,FALSE,FALSE,NULL,'lần','lần'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Gắn phục hình, giao hàm');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Tẩy trắng răng tại nhà',25,TRUE,FALSE,FALSE,1400000,'2 hàm','2 hàm'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Tẩy trắng răng tại nhà');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Tẩy trắng răng tại ghế',75,TRUE,FALSE,FALSE,2250000,'2 hàm','2 hàm'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Tẩy trắng răng tại ghế');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Niềng răng mắc cài kim loại (ca đơn giản)',90,TRUE,FALSE,FALSE,32500000,'ca','ca'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Niềng răng mắc cài kim loại (ca đơn giản)');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Tái khám siết niềng răng',25,TRUE,FALSE,FALSE,NULL,'lần','lần'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Tái khám siết niềng răng');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Hàm duy trì sau niềng',20,TRUE,FALSE,FALSE,1500000,'hàm','hàm'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Hàm duy trì sau niềng');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Cắm trụ Implant (ca đơn giản)',60,TRUE,FALSE,FALSE,14000000,'trụ','trụ'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Cắm trụ Implant (ca đơn giản)');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Abutment Implant',30,TRUE,FALSE,FALSE,6500000,'cái','cái'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Abutment Implant');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Mão răng trên Implant',45,TRUE,FALSE,FALSE,6750000,'răng','răng'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Mão răng trên Implant');
INSERT INTO "SCHEDULING_SERVICE" ("BranchId","Name","PublicDurationMinutes","Active","PublicBookable","IsInitialExamination","ReferenceUnitPriceVnd","Unit","BillingUnit")
SELECT ctx.branch_id,'Tái khám',20,TRUE,FALSE,FALSE,NULL,'lần','lần'
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "SCHEDULING_SERVICE" x WHERE x."BranchId"=ctx.branch_id AND x."Name"='Tái khám');

-- Kế hoạch điều trị mẫu (ItemPlan) và dòng kế hoạch
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Sâu răng đơn giản 1-2 mặt',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Sâu răng đơn giản 1-2 mặt' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám và chụp phim quanh chóp nếu cần.',0),
  (2,'Trám răng composite',35,'1 buổi. Nếu đau tự phát hoặc sâu vào tủy thì chuyển sang kế hoạch điều trị tủy.',400000)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Sâu răng đơn giản 1-2 mặt' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Điều trị tủy răng 1 chân và bọc mão',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Điều trị tủy răng 1 chân và bọc mão' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, hỏi bệnh sử, chụp phim.',0),
  (2,'Mở tủy giảm đau',30,'Buổi 1, kê thuốc giảm đau nếu cần.',0),
  (3,'Điều trị tủy răng 1 chân',60,'Buổi 2-3: sửa soạn và trám bít ống tủy.',1000000),
  (4,'Chốt sợi, cùi giả',30,'Làm cùi nếu răng mất nhiều mô.',1500000),
  (5,'Lấy dấu, thử khung phục hình',45,'Lấy dấu để gửi labo.',0),
  (6,'Mão răng sứ kim loại',60,'Giá có thể đổi theo vật liệu mão.',2750000),
  (7,'Gắn phục hình, giao hàm',30,'Gắn mão, chỉnh khớp cắn.',0),
  (8,'Tái khám',20,'Tái khám sau 1-2 tuần.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Điều trị tủy răng 1 chân và bọc mão' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Điều trị tủy răng hàm và bọc mão',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Điều trị tủy răng hàm và bọc mão' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, chụp phim quanh chóp.',0),
  (2,'Mở tủy giảm đau',30,'Buổi 1.',0),
  (3,'Điều trị tủy răng hàm lớn',90,'2-3 buổi, trám tạm giữa các buổi.',2900000),
  (4,'Chốt sợi, cùi giả',30,'Phục hồi cùi sau tủy.',1500000),
  (5,'Lấy dấu, thử khung phục hình',45,'Lấy dấu gửi labo.',0),
  (6,'Mão răng toàn sứ Zirconia',60,'Có thể thay bằng sứ kim loại tùy nhu cầu.',6250000),
  (7,'Gắn phục hình, giao hàm',30,'Gắn mão.',0),
  (8,'Tái khám',20,'Tái khám sau 1-2 tuần.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Điều trị tủy răng hàm và bọc mão' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Cạo vôi răng và viêm lợi nhẹ',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Cạo vôi răng và viêm lợi nhẹ' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, đánh giá túi nha chu.',0),
  (2,'Cạo vôi răng, đánh bóng',40,'Kèm hướng dẫn vệ sinh răng miệng.',300000),
  (3,'Tái khám',20,'Tái khám sau 2-4 tuần, sau đó 6 tháng/lần.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Cạo vôi răng và viêm lợi nhẹ' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Nhổ răng khôn mọc lệch nhẹ',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Nhổ răng khôn mọc lệch nhẹ' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám và chụp phim panorama.',0),
  (2,'Chụp phim panorama',10,'Đánh giá hướng mọc của răng khôn.',250000),
  (3,'Nhổ răng khôn hàm dưới độ 1-2',60,'1 buổi. Hàm trên dùng dịch vụ nhổ răng khôn hàm trên.',2200000),
  (4,'Cắt chỉ, tái khám sau nhổ',15,'Sau 5-7 ngày.',100000)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Nhổ răng khôn mọc lệch nhẹ' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Mất 1 răng: làm implant đơn lẻ',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Mất 1 răng: làm implant đơn lẻ' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, cạo vôi trước nếu cần.',0),
  (2,'Chụp CT Cone Beam',15,'Đánh giá xương.',0),
  (3,'Cắm trụ Implant (ca đơn giản)',60,'Buổi đặt trụ, cắt chỉ sau 7 ngày. Chờ tích hợp xương khoảng 3 tháng hàm dưới, 6 tháng hàm trên.',14000000),
  (4,'Abutment Implant',30,'Sau khi trụ tích hợp xương.',6500000),
  (5,'Lấy dấu, thử khung phục hình',45,'Lấy dấu gửi labo.',0),
  (6,'Mão răng trên Implant',45,'Giá phụ thuộc vật liệu mão.',6750000),
  (7,'Gắn phục hình, giao hàm',30,'Gắn mão, chỉnh khớp cắn.',0),
  (8,'Tái khám',20,'Tái khám định kỳ.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Mất 1 răng: làm implant đơn lẻ' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Mất 1 răng: làm cầu răng sứ',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Mất 1 răng: làm cầu răng sứ' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, cạo vôi, điều trị bệnh nha chu trước nếu có.',0),
  (2,'Cầu răng sứ kim loại',60,'Mài 2 răng trụ (buổi 1). Số đơn vị: 3 (2 trụ và 1 răng thay), giá là 3 lần giá một đơn vị.',8250000),
  (3,'Lấy dấu, thử khung phục hình',45,'Lấy dấu và gắn cầu tạm, thử khung.',0),
  (4,'Gắn phục hình, giao hàm',30,'Gắn cầu sứ.',0),
  (5,'Tái khám',20,'Tái khám sau 1-2 tuần.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Mất 1 răng: làm cầu răng sứ' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Mất nhiều răng: hàm tháo lắp',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Mất nhiều răng: hàm tháo lắp' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, đánh giá sống hàm, niêm mạc.',0),
  (2,'Lấy dấu, thử khung phục hình',45,'Lấy dấu sơ khởi, dấu cá nhân và ghi khớp cắn, thử răng sáp (khoảng 3 buổi).',0),
  (3,'Hàm tháo lắp nhựa',30,'Có thể chọn hàm khung kim loại.',3000000),
  (4,'Gắn phục hình, giao hàm',30,'Giao hàm và hướng dẫn sử dụng.',0),
  (5,'Tái khám',20,'Chỉnh đau 1-2 lần.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Mất nhiều răng: hàm tháo lắp' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Tẩy trắng răng',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Tẩy trắng răng' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, cạo vôi, trám sâu trước khi tẩy.',0),
  (2,'Cạo vôi răng, đánh bóng',40,'Làm sạch trước tẩy trắng.',300000),
  (3,'Tẩy trắng răng tại ghế',75,'Hoặc tẩy trắng tại nhà (giao khay).',2250000),
  (4,'Tái khám',20,'Tái khám sau 1-2 tuần. Không nên tẩy khi có thai, men nhạy cảm nặng hoặc răng trước có trám/mão.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Tẩy trắng răng' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Trẻ em: sâu răng sữa',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Trẻ em: sâu răng sữa' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, hướng dẫn phụ huynh.',0),
  (2,'Trám răng sữa',25,'20-30 phút/răng, 1-2 buổi.',300000),
  (3,'Lấy tủy răng sữa và trám',40,'Chỉ khi viêm tủy: cắt tủy buồng hoặc lấy tủy toàn phần tùy tình trạng.',750000),
  (4,'Bôi fluor',15,'Phòng sâu răng.',300000),
  (5,'Tái khám',20,'Mỗi 3-6 tháng.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Trẻ em: sâu răng sữa' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Niềng răng ca đơn giản',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Niềng răng ca đơn giản' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, chụp phim, lấy dấu, tư vấn.',0),
  (2,'Cạo vôi răng, đánh bóng',40,'Cạo vôi, trám sâu trước khi gắn mắc cài.',300000),
  (3,'Niềng răng mắc cài kim loại (ca đơn giản)',90,'Gắn mắc cài (khoảng 90 phút). Thời gian điều trị trung bình 18-24 tháng.',32500000),
  (4,'Tái khám siết niềng răng',25,'Tái khám 4-6 tuần/lần, khoảng 18-24 lần.',0),
  (5,'Hàm duy trì sau niềng',20,'Tháo mắc cài và đeo hàm duy trì.',1500000)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Niềng răng ca đơn giản' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Răng vỡ lớn đã chữa tủy: bọc mão',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Răng vỡ lớn đã chữa tủy: bọc mão' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, chụp phim.',0),
  (2,'Chốt sợi, cùi giả',30,'Dựng cùi.',1500000),
  (3,'Lấy dấu, thử khung phục hình',45,'Lấy dấu gửi labo.',0),
  (4,'Mão răng sứ kim loại',60,'Hoặc toàn sứ theo nhu cầu.',2750000),
  (5,'Gắn phục hình, giao hàm',30,'Gắn mão.',0),
  (6,'Tái khám',20,'Tái khám sau 1-2 tuần.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Răng vỡ lớn đã chữa tủy: bọc mão' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Sâu nhiều răng',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Sâu nhiều răng' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, ưu tiên răng đau hoặc gần tủy.',0),
  (2,'Trám răng composite',35,'Trám 2-4 răng mỗi buổi, 3-5 buổi. Răng đã viêm tủy thì chuyển kế hoạch điều trị tủy.',1600000),
  (3,'Cạo vôi răng, đánh bóng',40,'Làm sạch.',300000),
  (4,'Bôi fluor',15,'Phòng sâu răng, tư vấn chế độ ăn.',300000),
  (5,'Tái khám',20,'Tái khám sau 3 tháng.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Sâu nhiều răng' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Đau cấp: viêm tủy hoặc áp xe',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Đau cấp: viêm tủy hoặc áp xe' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, chụp phim quanh chóp.',0),
  (2,'Mở tủy giảm đau',30,'Mở tủy, kê thuốc.',0),
  (3,'Rạch dẫn lưu áp xe lợi',30,'Chỉ khi có ổ áp xe lợi khu trú.',900000),
  (4,'Tái khám',20,'Tái khám sau 24-48 giờ, rồi vào kế hoạch điều trị tủy hoặc nhổ răng.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Đau cấp: viêm tủy hoặc áp xe' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Nhổ răng không giữ được rồi phục hình',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Nhổ răng không giữ được rồi phục hình' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, hỏi bệnh sử (chống đông, đái tháo đường).',0),
  (2,'Nhổ răng đơn giản',25,'1 buổi.',350000),
  (3,'Cắt chỉ, tái khám sau nhổ',15,'Sau 7 ngày. Chờ lành thương 4-8 tuần tùy vị trí (ước lượng).',100000),
  (4,'Tái khám',20,'Sau đó chuyển sang kế hoạch implant, cầu răng hoặc hàm tháo lắp.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Nhổ răng không giữ được rồi phục hình' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Duy trì định kỳ 6 tháng',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Duy trì định kỳ 6 tháng' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám định kỳ.',0),
  (2,'Cạo vôi răng, đánh bóng',40,'Mỗi 6 tháng (3-4 tháng nếu có bệnh nha chu).',300000),
  (3,'Tái khám',20,'Hẹn lần sau.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Duy trì định kỳ 6 tháng' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");
INSERT INTO "TREATMENT_TEMPLATE" ("Id","BranchId","Name","Version","CreatedBy","CreatedAt","DefinitionKind")
SELECT gen_random_uuid(),ctx.branch_id,'Răng cửa thẩm mỹ: mặt dán sứ',1,ctx.actor_id,now(),'ItemPlan' FROM seed_ctx ctx
WHERE NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE" t WHERE t."BranchId"=ctx.branch_id AND t."Name"='Răng cửa thẩm mỹ: mặt dán sứ' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL);
INSERT INTO "TREATMENT_TEMPLATE_ITEM" ("Id","TemplateId","Position","TeethJson","ServiceId","ServiceName","EstimatedMinutes","Note","CostVnd")
SELECT gen_random_uuid(),t."Id",v.pos,'[]'::jsonb,s."Id",s."Name",v.minutes,v.note,v.cost
FROM "TREATMENT_TEMPLATE" t JOIN seed_ctx ctx ON t."BranchId"=ctx.branch_id
JOIN (VALUES
  (1,'Khám',30,'Khám, chụp phim, chọn màu.',0),
  (2,'Cạo vôi răng, đánh bóng',40,'Làm sạch trước.',300000),
  (3,'Mặt dán sứ Veneer',75,'2 răng trước, mài và lấy dấu. Giá phụ thuộc vật liệu.',18000000),
  (4,'Lấy dấu, thử khung phục hình',45,'Thử veneer.',0),
  (5,'Gắn phục hình, giao hàm',30,'Gắn veneer.',0),
  (6,'Tái khám',20,'Tái khám sau 2 tuần.',0)
) AS v(pos,service,minutes,note,cost) ON TRUE
JOIN "SCHEDULING_SERVICE" s ON s."BranchId"=ctx.branch_id AND s."Name"=v.service
WHERE t."Name"='Răng cửa thẩm mỹ: mặt dán sứ' AND t."DefinitionKind"='ItemPlan' AND t."DeletedAt" IS NULL
AND NOT EXISTS (SELECT 1 FROM "TREATMENT_TEMPLATE_ITEM" i WHERE i."TemplateId"=t."Id");

-- Công ty labo
INSERT INTO "CATALOG_PROVIDER" ("Id","Code","Name","NormalizedName","Type","IsActive","Version","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
SELECT gen_random_uuid(),'LAB-01','Xưởng labo mẫu 1','XƯỞNG LABO MẪU 1','Lab',TRUE,1,now(),ctx.actor_id,now(),ctx.actor_id
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "CATALOG_PROVIDER" WHERE "Code"='LAB-01');
INSERT INTO "CATALOG_PROVIDER" ("Id","Code","Name","NormalizedName","Type","IsActive","Version","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
SELECT gen_random_uuid(),'LAB-02','Xưởng labo mẫu 2','XƯỞNG LABO MẪU 2','Lab',TRUE,1,now(),ctx.actor_id,now(),ctx.actor_id
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "CATALOG_PROVIDER" WHERE "Code"='LAB-02');

-- Loại công việc labo
INSERT INTO "LAB_WORK_TYPE" ("Id","Code","Name","NormalizedName","ProviderId","Unit","IsActive","Version","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
SELECT gen_random_uuid(),'LW-01','Mão sứ kim loại','MÃO SỨ KIM LOẠI',(SELECT "Id" FROM "CATALOG_PROVIDER" WHERE "Code"='LAB-01'),'Tooth',TRUE,1,now(),ctx.actor_id,now(),ctx.actor_id
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "LAB_WORK_TYPE" WHERE "NormalizedName"='MÃO SỨ KIM LOẠI');
INSERT INTO "LAB_WORK_TYPE" ("Id","Code","Name","NormalizedName","ProviderId","Unit","IsActive","Version","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
SELECT gen_random_uuid(),'LW-02','Mão toàn sứ Zirconia','MÃO TOÀN SỨ ZIRCONIA',(SELECT "Id" FROM "CATALOG_PROVIDER" WHERE "Code"='LAB-02'),'Tooth',TRUE,1,now(),ctx.actor_id,now(),ctx.actor_id
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "LAB_WORK_TYPE" WHERE "NormalizedName"='MÃO TOÀN SỨ ZIRCONIA');
INSERT INTO "LAB_WORK_TYPE" ("Id","Code","Name","NormalizedName","ProviderId","Unit","IsActive","Version","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
SELECT gen_random_uuid(),'LW-03','Mão trên Implant','MÃO TRÊN IMPLANT',(SELECT "Id" FROM "CATALOG_PROVIDER" WHERE "Code"='LAB-01'),'Tooth',TRUE,1,now(),ctx.actor_id,now(),ctx.actor_id
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "LAB_WORK_TYPE" WHERE "NormalizedName"='MÃO TRÊN IMPLANT');
INSERT INTO "LAB_WORK_TYPE" ("Id","Code","Name","NormalizedName","ProviderId","Unit","IsActive","Version","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
SELECT gen_random_uuid(),'LW-04','Cầu răng sứ kim loại','CẦU RĂNG SỨ KIM LOẠI',(SELECT "Id" FROM "CATALOG_PROVIDER" WHERE "Code"='LAB-02'),'Tooth',TRUE,1,now(),ctx.actor_id,now(),ctx.actor_id
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "LAB_WORK_TYPE" WHERE "NormalizedName"='CẦU RĂNG SỨ KIM LOẠI');
INSERT INTO "LAB_WORK_TYPE" ("Id","Code","Name","NormalizedName","ProviderId","Unit","IsActive","Version","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
SELECT gen_random_uuid(),'LW-05','Hàm tháo lắp nhựa','HÀM THÁO LẮP NHỰA',(SELECT "Id" FROM "CATALOG_PROVIDER" WHERE "Code"='LAB-01'),'Jaw',TRUE,1,now(),ctx.actor_id,now(),ctx.actor_id
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "LAB_WORK_TYPE" WHERE "NormalizedName"='HÀM THÁO LẮP NHỰA');
INSERT INTO "LAB_WORK_TYPE" ("Id","Code","Name","NormalizedName","ProviderId","Unit","IsActive","Version","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
SELECT gen_random_uuid(),'LW-06','Mặt dán sứ Veneer','MẶT DÁN SỨ VENEER',(SELECT "Id" FROM "CATALOG_PROVIDER" WHERE "Code"='LAB-02'),'Tooth',TRUE,1,now(),ctx.actor_id,now(),ctx.actor_id
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "LAB_WORK_TYPE" WHERE "NormalizedName"='MẶT DÁN SỨ VENEER');
INSERT INTO "LAB_WORK_TYPE" ("Id","Code","Name","NormalizedName","ProviderId","Unit","IsActive","Version","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
SELECT gen_random_uuid(),'LW-07','Hàm duy trì sau niềng','HÀM DUY TRÌ SAU NIỀNG',(SELECT "Id" FROM "CATALOG_PROVIDER" WHERE "Code"='LAB-01'),'Jaw',TRUE,1,now(),ctx.actor_id,now(),ctx.actor_id
FROM seed_ctx ctx WHERE NOT EXISTS (SELECT 1 FROM "LAB_WORK_TYPE" WHERE "NormalizedName"='HÀM DUY TRÌ SAU NIỀNG');

COMMIT;
