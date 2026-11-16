# Arena — نظام إدارة بطولات وفعاليات كرة القدم

حزمة الأسبوع الأول لفريق من 4 أفراد: تحليل النظام، EERD وMapping، قاعدة بيانات EF Core Code First، وWeb API يطبق Repository وUnit of Work. تشمل نسخة عملية للفرق واللاعبين والبطولات والمباريات والنتائج والترتيب والإحصائيات وحجز التذاكر.

## ابدأ هنا

1. اقرأ `docs/01_System_Analysis.docx` مع الفريق واعتمد النطاق وحالات الاستخدام.
2. راجع `docs/02_Database_Design_and_Mapping.docx` والعلاقات في `docs/diagrams`.
3. افتح `Arena.sln` في محرر يدعم .NET 10، أو استخدم الطرفية.
4. شغّل نسخة العرض التالية، ثم جرّب `requests/Arena.http` أو صفحة Scalar.

## تشغيل العرض محليًا

المطلوب: .NET SDK 10 أو أحدث مع ASP.NET Core Runtime 10، وPowerShell. أول بناء يحتاج اتصال NuGet. يستهدف الحل `net10.0`، و`global.json` يسمح باستخدام SDK أحدث عند غياب 10. جميع أوامر هذا الدليل من مجلد المشروع.

```powershell
Set-Location 'D:\EraaSoft\Project'
.\scripts\start-demo.ps1
```

سيطلب السكربت كلمة مرور Admin من 12 إلى 128 حرفًا دون إظهارها، ويبني المشروع وينشئ قاعدة SQLite في `artifacts/arena-demo.db` ويضيف بيانات العرض. البريد الافتراضي `leader@arena.local`؛ كلمة المرور هي التي أدخلتها. إذا كان الحساب موجودًا فلن تتغير كلمة مروره عند إعادة التشغيل.

- الصفحة التفاعلية: http://localhost:5080/scalar/v1
- ملف عقد API أثناء التشغيل: http://localhost:5080/openapi/v1.json
- فحص الاتصال بالقاعدة: http://localhost:5080/health/ready
- للإيقاف: `Ctrl+C`. لتغيير المنفذ استخدم `-Port 5090`.

ينشئ السكربت مفتاح JWT عشوائيًا للجلسة إذا لم توفر `Jwt__SigningKey`. لذلك يلزم تسجيل الدخول مجددًا بعد إعادة التشغيل. بيانات العرض تنشأ مرة واحدة؛ مواعيدها مرتبطة بيوم إنشائها. بعد انقضاء المواعيد أنشئ فعاليات جديدة أو احتفظ بنسخة من قاعدة العرض ثم ابدأ قاعدة جديدة.

## SQL Server: قاعدة التسليم الأساسية

النهج المعتمد **Code First**، لذلك لا توجد خطوة Scaffold مطلوبة. SQL Server هو مزود النشر؛ SQLite مخصص للتطوير والاختبارات.

```powershell
.\scripts\setup-sqlserver.ps1 -SeedDemo
```

الاتصال الافتراضي `Server=.;Database=ArenaSports;Trusted_Connection=True;TrustServerCertificate=True`. لو كانت نسختك Express:

```powershell
.\scripts\setup-sqlserver.ps1 -ConnectionString 'Server=.\SQLEXPRESS;Database=ArenaSports;Trusted_Connection=True;TrustServerCertificate=True' -SeedDemo
```

يطبق السكربت Migration ويهيئ Admin ويضيف بيانات العرض عند طلبها. يجب تشغيل خدمة SQL Server والسماح لحسابك بإنشاء القاعدة. `TrustServerCertificate=True` إعداد محلي فقط؛ استخدم شهادة موثوقة عند النشر.

بعد التهيئة، شغّل API على SQL Server:

```powershell
.\scripts\start-sqlserver.ps1
# أو مرر نفس ConnectionString التي استخدمتها في التهيئة
```

يمكن تطبيق `database/01_InitialCreate.sql` من SSMS على **قاعدة ArenaSports فارغة تختارها أولًا**. السكربت idempotent ويتابع `__EFMigrationsHistory`؛ لا ينشئ قاعدة البيانات نفسها ولا يضيف Admin. استخدم سكربت التهيئة بعده لإنشاء Admin دون إعادة إنشاء الجداول. ثم نفذ `database/02_VerifySchema.sql` لفحص الجداول والمفاتيح والقيود.

**حالة التحقق:** طُبقت Migration وسكربت `01_InitialCreate.sql` (مرتين، للتأكد من أنه idempotent) على SQL Server 2025 LocalDB: 14 جدولًا و21 مفتاحًا أجنبيًا و19 قيد CHECK. اختبار التزامن نجح على نفس المحرك (القسم التالي). باقي تطبيق القاعدة على خادم الفريق نفسه.

### اختبار التزامن على SQL Server

```powershell
.\scripts\test-concurrency.ps1
# أو على خادم آخر وبحمل أكبر:
.\scripts\test-concurrency.ps1 -Server '.' -Requests 100 -Capacity 30
```

ينشئ قاعدة مؤقتة، ويشغل API عليها، ويرسل طلبات في نفس اللحظة، ثم يحذف القاعدة. يفحص:
- أكثر من مشترٍ على مخزون قليل: ينجح عدد يساوي المخزون بالضبط، والباقي 409 `sold_out`، ولا يوجد 500.
- نفس الطلب بنفس `IdempotencyKey` عدة مرات معًا: حجز واحد وخصم مخزون مرة واحدة.
- إلغاء نفس الحجز عدة مرات معًا: المخزون يرجع مرة واحدة.

النتيجة المسجلة على LocalDB: 20 طلبًا على 5 تذاكر، و100 طلب على 30 تذكرة، كلها ناجحة دون أي 500.

## بنية الحل

```text
src/
  Arena.Domain/
    Common/Entity.cs            المفتاح وCreatedUtc وVersion لكل جدول
    Entities/                   كيان واحد في كل ملف (16 كيان)
    Enums/                      حالات النظام والأدوار
  Arena.Application/
    Common/                     ServiceBase وGuard وBusinessException وPageResult
    Interfaces/                 IRepository وIUnitOfWork وICompetitionQueries
    Features/<Feature>/         خدمة الـFeature وطلباتها وردودها (DTOs)
      Auth, Teams, Players, Tournaments, Venues, Events, Results, Tickets, Bookings
  Arena.Infrastructure/
    Persistence/                ArenaDbContext وUnitOfWork وDesignTimeFactory
    Persistence/Configurations/ Fluent Mapping لكل جدول في ملف
    Repositories/EfRepository.cs
    Queries/CompetitionQueries.cs  الترتيب والهدافين والتعارض والمخزون
    Migrations/
  Arena.Api/
    Controllers/                Controller واحد في كل ملف
    Auth/ Errors/ Documentation/ JWT ومعالجة الأخطاء وOpenAPI
    Setup/                      تسجيل الخدمات (DI) والتهيئة وتصدير المخطط
tests/Arena.Tests/              اختبارات المنطق والعلاقات وHTTP
database/                       سكربت SQL وتصدير النموذج الفعلي
docs/                           التحليل والتصميم والمخططات وعقد OpenAPI
scripts/                        تشغيل العرض وتهيئة SQL Server والتحقق
requests/Arena.http             طلبات جاهزة للتجربة
```

اتجاه الاعتماد: Application تعتمد على Domain؛ Infrastructure تنفذ واجهات Application؛ Api تجمع الاعتمادات وتستدعي الخدمات. Controllers لا تستخدم DbContext مباشرة، ولا تبني شروط بحث؛ كل Controller يستدعي خدمة الـFeature بتاعته.

### ملكية الملفات داخل الفريق

كل Feature في فولدر مستقل، فكل عضو يشتغل غالبًا في فولدرات مختلفة ويقل التعارض عند الدمج.

| العضو | Features | Controllers |
|---|---|---|
| الثاني | Teams, Players | TeamsController, PlayersController |
| الثالث | Tournaments, Venues, Events, Results | TournamentsController, VenuesController, EventsController, ResultsController |
| الرابع | Tickets, Bookings, Auth | TicketsController, BookingsController, AuthController |
| قائد الفريق | Common, Interfaces, Infrastructure, Setup, Migrations | مراجعة أي تغيير في الموديل أو الـMigration |

- `IRepository<T>` و`EfRepository<T>`: عمليات القراءة والإضافة دون كشف `IQueryable` للـAPI.
- `ServiceBase`: الأساس المشترك لخدمات الـFeatures (الوصول للمستودعات، الوقت الحالي، 404، والتقسيم لصفحات).
- `ICompetitionQueries`: استعلامات الترتيب والإحصائيات المتخصصة.
- `IUnitOfWork`: مشاركة DbContext واحد وحفظ العملية كاملة في معاملة واحدة.
- `ITicketInventory`: يخصم المخزون ويرجعه بأمر `UPDATE` شرطي واحد (`WHERE Capacity - SoldQuantity >= @n`)، فقاعدة البيانات تفحص وتعدل في نفس اللحظة. الطلبات المتزامنة تنتظر قفل الصف بدل ما تعمل deadlock.
- `InTransactionAsync` (ReadCommitted): للحجز والإلغاء، لأن الصحة فيهما من الـUPDATE الشرطي والـVersion والفهرس الفريد.
- `SerializableAsync`: للقواعد اللي بتفحص صفوف لسه مش موجودة، زي تعارض المواعيد ومجموع سعات العروض. `Version` يحمي من الكتابة بنسخة قديمة.
- يوجد مثال عملي كامل داخل مسار إنشاء الحجز: تحقق من المفتاح والمخزون، تحديث المخزون، إنشاء الحجز والتذاكر، ثم commit.

## قواعد النسخة الحالية

- كرة القدم فقط، دوري بنقاط 3/1/0. الترتيب: النقاط ثم فرق الأهداف ثم الأهداف المسجلة؛ TeamId للفصل الثابت في العرض، وليس قاعدة رياضية لحسم لقب متعادل.
- 14 جدولًا. `Events` يستخدم TPH للتخصص الكلي والمنفصل: `MatchEvent` أو `CommunityEvent`.
- سجل عضويات مستقل عن قائمة البطولة؛ اللاعب يمثل فريقًا واحدًا في البطولة.
- النتائج المعتمدة فقط تؤثر على الترتيب؛ التصحيح يحفظ سجل تدقيق.
- الحجز 1–10 تذاكر، مع `IdempotencyKey` فريد لكل مستخدم. إعادة الطلب بنفس الجسم تعيد نفس الحجز؛ اختلاف الجسم بنفس المفتاح يعيد 409.
- الحجز المؤكد **حجز تذاكر فقط**، ولا يعني دفعًا ماليًا. الأسعار بالجنيه المصري.
- الإلغاء قبل بداية الفعالية بساعتين يعيد المخزون مرة واحدة. فحص التذكرة من ساعتين قبل البداية حتى نهاية الفعالية، لمرة واحدة.
- التاريخ والوقت في API بصيغة UTC مثل `2026-11-20T16:00:00Z`، والسعر بحد أقصى منزلتين عشريتين.
- القوائم مقسمة صفحات: `page=1&pageSize=20`، بحد أقصى 100.
- حقول `Version` في التحديث تؤخذ من أحدث GET؛ استجابة 409 تعني إعادة القراءة ثم اتخاذ قرار إعادة المحاولة.

## الحسابات والإعدادات

التسجيل العام ينشئ Spectator فقط. Admin وOrganizer يديران الرياضة والتذاكر؛ GateStaff يفحص الدخول. Organizer موظف موثوق على مستوى النظام كله. TeamManager موجود في النموذج، وعملياته الذاتية ضمن الاستكمال.

| المتغير | الغرض |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | Development محليًا؛ Production للنشر |
| `Database__Provider` | SqlServer افتراضيًا؛ Sqlite في Development/Testing فقط |
| `ConnectionStrings__Arena` | اتصال القاعدة |
| `Jwt__SigningKey` | سر عشوائي لا يقل عن 32 بايت، مطلوب لتشغيل الخادم |
| `Bootstrap__AdminEmail` و`Bootstrap__AdminPassword` | تهيئة أول مدير؛ لا تضع القيم في Git |

Scalar وOpenAPI متاحان في Development فقط. لا توجد كلمات مرور أو مفاتيح حقيقية في الحزمة. للنشر: وفر سرًا ثابتًا عبر مخزن أسرار، واضبط HTTPS وAllowedHosts والاتصال والنسخ الاحتياطي. خلف reverse proxy يلزم إعداد forwarded headers الموثوق قبل الاعتماد على محدد الطلبات حسب عنوان العميل.

## الاختبارات وتطور قاعدة البيانات

```powershell
.\scripts\verify.ps1
```

يشغل restore وbuild والاختبارات وفحص اختلاف الموديل. النتيجة المسجلة: **23 اختبارًا ناجحًا**. تقرير TRX في `TestResults`، وتقرير التجربة الحية في `TestResults/smoke-results.json`.

```powershell
dotnet tool restore
dotnet ef migrations add ChangeName --project src/Arena.Infrastructure --startup-project src/Arena.Api
dotnet ef database update --project src/Arena.Infrastructure --startup-project src/Arena.Api
dotnet ef migrations script --idempotent --project src/Arena.Infrastructure --startup-project src/Arena.Api --output database/01_InitialCreate.sql
```

لا تضف Migration جديدة عند فتح المشروع لأول مرة؛ طبق الموجودة. لا تعدل Migration سبق تطبيقها على قاعدة مشتركة. اجعل عضوًا واحدًا يراجع تغيرات الموديل وادمج الفرع الرئيسي قبل التوليد. CI في `.github/workflows/ci.yml` جاهز للبناء والاختبار على GitHub، ولم يتم تشغيله على GitHub ضمن هذه الجلسة.

## تجربة المناقشة

1. شغّل العرض وافتح `/api/v1/tournaments/1/standings` و`/scorers`.
2. سجل مستخدم جمهور، وانسخ `accessToken` إلى Bearer في Scalar أو `Arena.http`.
3. اعرض فئات تذاكر الفعالية المستقبلية ثم احجز تذكرتين بالمفتاح نفسه مرتين؛ يجب أن يعود نفس الحجز.
4. ألغ الحجز وافحص عودة المخزون؛ كرر الإلغاء للتأكد من عدم الزيادة مرتين.
5. سجل دخول Admin لعرض إنشاء الفرق والجدولة وتصحيح النتائج.
6. افتح `BookingService` و`UnitOfWork` وMigration واشرح القواعد ثم شغّل الاختبارات.

## ما يستكمله الفريق

مهام الأسبوع الأول لها ملفات وتنفيذ حاضر. المنتج النهائي يتبعه: إدارة الموظفين، صلاحيات مدير الفريق الذاتية، إعادة جدولة وإلغاء الفعاليات، إغلاق البطولة، البحث والتصدير واستعادة كلمة المرور. راجع حالة كل Feature في `docs/Requirements_Traceability.md`؛ لا تقدم هذه الوظائف كمنفذة الآن.

قبل المناقشة: اجتمعوا لاعتماد التحليل وتسجيل أسماء المالكين، طبقوا SQL Server على جهاز متاح، شغّلوا `scripts/test-concurrency.ps1` عليه، ثم جرّب كل عضو تشغيل الحزمة وشرح وحدته. خطة توزيع الفريق والسبعة أيام في ملف التحليل.
