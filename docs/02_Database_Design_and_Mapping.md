تصميم قاعدة البيانات والعلاقات والتحويل العلائقي

تسليم الأسبوع الأول   16 إلى 22 نوفمبر 2026

C# Web API  ·  .NET 10  ·  EF Core  ·  SQL Server

يوثق هذا المستند نموذج EERD والتحويل إلى جداول SQL Server الفعلية. أسماء الجداول والأعمدة والمفاتيح مأخوذة من نموذج EF Core المصدّر في database/schema.json، وهو مرجع تدقيق لتطابق الكود مع التصميم. راجع ملف التحليل لفهم حالات الاستخدام وقواعد الصلاحيات.

الفكرة العامة

تعالج الجداول العلاقات متعددة الأطراف من خلال جداول ربط صريحة. تاريخ انتقال اللاعب مستقل عن قائمة البطولة. المباراة نوع من الفعالية، بحيث ترتبط التذاكر بأي فعالية دون وجود مفتاحين اختياريين متنافسين. الترتيب والمجاميع الرياضية بيانات مشتقة ولا تخزن في جدول مستقل.

المجموعة | الجداول
--- | ---
الحسابات والفرق | Users, Teams, Players, PlayerMemberships
البطولات | Tournaments, TournamentTeams, TournamentRosters
الأحداث والنتائج | Venues, Events, MatchPlayerStats, ResultAudits
التذاكر | TicketOffers, Bookings, Tickets

قرارات التحويل

كل جدول له Id صحيح كمفتاح أساسي، وCreatedUtc، وVersion للتزامن. المفاتيح الطبيعية مثل البريد وكود الفريق تحفظ بقيود فريدة. علاقات الربط تحتفظ بمعرف بديل مع مفاتيح مركبة إضافية للرجوع إليها. سياسة الحذف Restrict في كل العلاقات للحفاظ على التاريخ.

١ التخصص في النموذج المحسن


## ١ التخصص في النموذج المحسن

التخصص كلي ومنفصل: كل SportEvent هو MatchEvent أو CommunityEvent فقط. التحويل المستخدم Table Per Hierarchy بجدول Events واحد وعمود EventType. يفرض CHECK وجود أعمدة المباراة وحدها أو وصف الفعالية وحده، وتمنع قاعدة Scores حالة Finished بلا نتيجتين للمباراة.

٢ علاقات الفرق والبطولات


## ٢ علاقات الفرق والبطولات

العضوية تعالج تاريخ العلاقة بين اللاعب والفريق، وقائمة البطولة تعالج اللاعب داخل مشاركة فريق محدد. لا يستخدم TeamId الحالي للاعب لإعادة تفسير بطولات سابقة. ترجع المباراة إلى TournamentTeams بمفتاح مركب لكل من صاحب الأرض والضيف.

المفتاح PK أساسي وFK أجنبي، وعلامة الاستفهام تعني أن العمود يقبل NULL. طرف الخط الواحد يمثل واحدًا وطرف التفرع يمثل متعددًا، والدائرة تمثل الاختيارية. المخطط الكامل والمتجهات SVG ومصدر Mermaid موجودة في docs/diagrams.

٣ علاقات الفعاليات والنتائج


## ٣ علاقات الفعاليات والنتائج

Events هو جدول الأساس ونقطة ربط الملاعب والنتائج. يُحفظ سجل مستقل لكل تعديل نتيجة. ترتبط إحصائية اللاعب بقائمة البطولة. يظهر Users في المخطط العام كمنظم للحدث أو فاعل للاعتماد، وحذف منظم العرض الفرعي لا يعني غياب هذه المفاتيح.

المفتاح PK أساسي وFK أجنبي، وعلامة الاستفهام تعني أن العمود يقبل NULL. طرف الخط الواحد يمثل واحدًا وطرف التفرع يمثل متعددًا، والدائرة تمثل الاختيارية. المخطط الكامل والمتجهات SVG ومصدر Mermaid موجودة في docs/diagrams.

٤ علاقات التذاكر والحجوزات


## ٤ علاقات التذاكر والحجوزات

العرض يخص فعالية واحدة، والحجز يخص مستخدمًا واحدًا وعرضًا واحدًا، وينشئ عددًا من التذاكر يساوي Quantity. السعر محفوظ بالحجز حتى إذا تغير سعر العرض. يحفظ فحص التذكرة المستخدم الذي أجرى الفحص ووقته.

المفتاح PK أساسي وFK أجنبي، وعلامة الاستفهام تعني أن العمود يقبل NULL. طرف الخط الواحد يمثل واحدًا وطرف التفرع يمثل متعددًا، والدائرة تمثل الاختيارية. المخطط الكامل والمتجهات SVG ومصدر Mermaid موجودة في docs/diagrams.

5 قاموس البيانات Users و Teams


## 5 قاموس البيانات Users و Teams

Users

حساب واحد بدور واحد؛ البريد بعد التطبيع مفتاح مرشح. PasswordHash لا يعود في استجابات API.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
CreatedUtc | datetime2 | No | 
Email | nvarchar(256) | No | 
FullName | nvarchar(120) | No | 
IsActive | bit | No | 
NormalizedEmail | nvarchar(256) | No | 
PasswordHash | nvarchar(512) | No | 
Role | nvarchar(30) | No | 
Version | uniqueidentifier | No | Concurrency

Unique: NormalizedEmail

Teams

كود ثابت وفريد مع مدير اختياري. IsActive للأرشفة دون حذف البيانات التاريخية.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
City | nvarchar(80) | No | 
Code | nvarchar(10) | No | 
CreatedUtc | datetime2 | No | 
IsActive | bit | No | 
ManagerId | int | Yes | FK
Name | nvarchar(120) | No | 
Version | uniqueidentifier | No | Concurrency

Unique: Code

6 قاموس البيانات Players و PlayerMemberships


## 6 قاموس البيانات Players و PlayerMemberships

Players

هوية اللاعب مستقلة عن فريقه الحالي وتاريخ العضويات. الاسم ليس مفتاحًا فريدًا.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
BirthDate | date | No | 
CreatedUtc | datetime2 | No | 
FullName | nvarchar(120) | No | 
Nationality | nvarchar(80) | No | 
Position | nvarchar(20) | No | 
Version | uniqueidentifier | No | Concurrency

PlayerMemberships

فترات عضوية اللاعب، بحيث تكون فترة واحدة فقط مفتوحة. النهاية غير شاملة.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
CreatedUtc | datetime2 | No | 
JoinedOn | date | No | 
LeftOn | date | Yes | 
PlayerId | int | No | FK
TeamId | int | No | FK
Version | uniqueidentifier | No | Concurrency

Unique: PlayerId WHERE [LeftOn] IS NULL

7 قاموس البيانات Tournaments و TournamentTeams


## 7 قاموس البيانات Tournaments و TournamentTeams

Tournaments

بطولة دوري ضمن موسم وفترة وتحت مسؤولية مستخدم منظم.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
CreatedUtc | datetime2 | No | 
EndDate | date | No | 
Name | nvarchar(160) | No | 
OrganizerId | int | No | FK
Season | nvarchar(30) | No | 
StartDate | date | No | 
Status | nvarchar(30) | No | 
Version | uniqueidentifier | No | Concurrency

Unique: Name,Season

TournamentTeams

جدول ربط المشاركة بين البطولة والفريق؛ المرجع الصحيح لطرف المباراة.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
CreatedUtc | datetime2 | No | 
TeamId | int | No | FK
TournamentId | int | No | FK
Version | uniqueidentifier | No | Concurrency

Unique: TournamentId,TeamId

8 قاموس البيانات TournamentRosters و Venues


## 8 قاموس البيانات TournamentRosters و Venues

TournamentRosters

قائمة ثابتة لكل بطولة؛ اللاعب لا يمثل أكثر من فريق فيها ورقم القميص لا يتكرر داخل الفريق.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
CreatedUtc | datetime2 | No | 
PlayerId | int | No | FK
ShirtNumber | int | No | 
TeamId | int | No | FK
TournamentId | int | No | FK
Version | uniqueidentifier | No | Concurrency

Unique: TournamentId,TeamId,PlayerId; TournamentId,PlayerId; TournamentId,TeamId,ShirtNumber

Venues

ملاعب وأماكن الفعاليات مع سعة إجمالية للدخول العام دون مقاعد مرقمة.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
Address | nvarchar(300) | No | 
Capacity | int | No | 
City | nvarchar(80) | No | 
CreatedUtc | datetime2 | No | 
Name | nvarchar(160) | No | 
Version | uniqueidentifier | No | Concurrency

9 قاموس البيانات Events


## 9 قاموس البيانات Events

Events

تحويل التخصص إلى جدول واحد. الأعمدة النوعية Nullable فعليًا لكن CHECK يفرضها حسب EventType.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
AwayScore | int | Yes | 
AwayTeamId | int | Yes | FK
CreatedUtc | datetime2 | No | 
Description | nvarchar(2000) | Yes | 
EndsAtUtc | datetime2 | No | 
EventType | nvarchar(20) | No | 
HomeScore | int | Yes | 
HomeTeamId | int | Yes | FK
Leg | int | Yes | 
OrganizerId | int | No | FK
RoundNumber | int | Yes | 
StartsAtUtc | datetime2 | No | 
Status | nvarchar(20) | No | 
Title | nvarchar(180) | No | 
TournamentId | int | Yes | FK
VenueId | int | No | FK
Version | uniqueidentifier | No | Concurrency

Unique: TournamentId,HomeTeamId,AwayTeamId,Leg WHERE [EventType] = 'Match'

10 قاموس البيانات MatchPlayerStats و ResultAudits


## 10 قاموس البيانات MatchPlayerStats و ResultAudits

MatchPlayerStats

إحصائية لاعب واحد في مباراة واحدة مع مرجع مركب لقائمة البطولة.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
Assists | int | No | 
CreatedUtc | datetime2 | No | 
Goals | int | No | 
MatchId | int | No | FK
MinutesPlayed | int | No | 
PlayerId | int | No | FK
RedCards | int | No | 
TeamId | int | No | FK
TournamentId | int | No | FK
Version | uniqueidentifier | No | Concurrency
YellowCards | int | No | 

Unique: MatchId,PlayerId

ResultAudits

سجل اعتماد أو تصحيح نتيجة؛ لا يغير المستخدم سجلات التدقيق السابقة عبر API.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
ActorId | int | No | FK
AwayScore | int | No | 
CreatedUtc | datetime2 | No | 
HomeScore | int | No | 
MatchId | int | No | FK
PreviousAwayScore | int | Yes | 
PreviousHomeScore | int | Yes | 
Reason | nvarchar(500) | No | 
Version | uniqueidentifier | No | Concurrency

11 قاموس البيانات TicketOffers و Bookings


## 11 قاموس البيانات TicketOffers و Bookings

TicketOffers

فئات المخزون والأسعار ونافذة البيع. SoldQuantity قيمة تشغيلية تحفظ ذريًا مع الحجوزات.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
Capacity | int | No | 
CreatedUtc | datetime2 | No | 
EventId | int | No | FK
Name | nvarchar(80) | No | 
Price | decimal(12,2) | No | 
SalesEndUtc | datetime2 | No | 
SalesStartUtc | datetime2 | No | 
SoldQuantity | int | No | 
Version | uniqueidentifier | No | Concurrency

Unique: EventId,Name

Bookings

حجز واحد لعرض واحد وسعر لقطة وكمية ومفتاح إعادة الطلب. لا يمثل تحصيلًا ماليًا.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
CancelledUtc | datetime2 | Yes | 
CreatedUtc | datetime2 | No | 
IdempotencyKey | nvarchar(64) | No | 
Quantity | int | No | 
Status | nvarchar(20) | No | 
TicketOfferId | int | No | FK
UnitPrice | decimal(12,2) | No | 
UserId | int | No | FK
Version | uniqueidentifier | No | Concurrency

Unique: UserId,IdempotencyKey

12 قاموس البيانات Tickets


## 12 قاموس البيانات Tickets

Tickets

تذكرة فردية بكود عشوائي فريد، وفحص واحد فقط. الإلغاء يقرأ من الحجز.

Column | SQL Server type | NULL | Key
--- | --- | --- | ---
Id | int | No | PK
BookingId | int | No | FK
CheckedInById | int | Yes | FK
CheckedInUtc | datetime2 | Yes | 
Code | nvarchar(64) | No | 
CreatedUtc | datetime2 | No | 
Version | uniqueidentifier | No | Concurrency

Unique: Code

١٣ سجل المفاتيح الأجنبية


## ١٣ سجل المفاتيح الأجنبية

Child table and columns | Parent table and columns | Parent per child
--- | --- | ---
Bookings
TicketOfferId | TicketOffers
Id | 1
Bookings
UserId | Users
Id | 1
Events
TournamentId, AwayTeamId | TournamentTeams
TournamentId, TeamId | 0..1
Events
TournamentId, HomeTeamId | TournamentTeams
TournamentId, TeamId | 0..1
Events
OrganizerId | Users
Id | 1
Events
VenueId | Venues
Id | 1
MatchPlayerStats
MatchId | Events
Id | 1
MatchPlayerStats
TournamentId, TeamId, PlayerId | TournamentRosters
TournamentId, TeamId, PlayerId | 1
PlayerMemberships
PlayerId | Players
Id | 1
PlayerMemberships
TeamId | Teams
Id | 1
ResultAudits
MatchId | Events
Id | 1

كل أصل يمكن أن يملك صفرًا إلى عدة سجلات تابعة. العدد الموجود في العمود الأخير يصف عدد الأصول لكل سجل تابع؛ جميع العلاقات تمنع الحذف التتابعي.

١٤ بقية العلاقات والقيود الدلالية


## ١٤ بقية العلاقات والقيود الدلالية

Child table and columns | Parent table and columns | Parent per child
--- | --- | ---
ResultAudits
ActorId | Users
Id | 1
Teams
ManagerId | Users
Id | 0..1
TicketOffers
EventId | Events
Id | 1
Tickets
BookingId | Bookings
Id | 1
Tickets
CheckedInById | Users
Id | 0..1
TournamentRosters
PlayerId | Players
Id | 1
TournamentRosters
TournamentId, TeamId | TournamentTeams
TournamentId, TeamId | 1
Tournaments
OrganizerId | Users
Id | 1
TournamentTeams
TeamId | Teams
Id | 1
TournamentTeams
TournamentId | Tournaments
Id | 1

قواعد لا يكفي لها مفتاح أجنبي

لا يثبت FK إلى Events وحده أن السجل المشار إليه مباراة، لأن الجدول يحتوي أكثر من نوع. تستخدم خدمة النتائج والإحصائيات MatchEvent فقط. كذلك تثبت الخدمة تطابق بطولة الإحصائية مع بطولة المباراة وأن فريق اللاعب أحد طرفيها. إجمالي سعات العروض، وتعارض المواعيد، وصلاحية موظف البوابة قواعد متعددة الصفوف تنفذ داخل المعاملات في التطبيق. لا يحتوي هذا التسليم على Triggers لفرض هذه القواعد على SQL الخام.

١٥ التطبيع والبيانات المشتقة


## ١٥ التطبيع والبيانات المشتقة

من الشكل المفاهيمي إلى العلائقي

العلاقة متعددة الأطراف بين البطولة والفريق تحولت إلى TournamentTeams. علاقة اللاعب بالبطولة والفريق تحولت إلى TournamentRosters مع قيد يحدد فريقًا واحدًا للاعب في البطولة. تاريخ اللاعب في الفرق تحول إلى PlayerMemberships؛ لهذا لا يوجد TeamId متغير داخل Players.

العلاقة 1 إلى متعدد تحمل FK في الطرف المتعدد. المدير اختياري في Teams، وموظف الفحص اختياري في Tickets قبل الدخول. الحدث نوع كلي منفصل ينفذ بعمود تمييز وقيود شرطية بدل ثلاثة جداول، وهو اختيار TPH مقصود لتوحيد مخزون الفعاليات.

سلامة التطبيع

الأسماء متصلة بمعرفاتها في جداولها الأساسية؛ لا تكرر بيانات الفريق أو المستخدم في الحجوزات والنتائج. تكرار مفاتيح البطولة والفريق في القوائم والإحصائيات مقصود لبناء مراجع مركبة وفحص الانتماء. يقابل ذلك قيد تطابق بطولة الإحصائية مع المباراة في التطبيق لتجنب تضارب تلك القيم.

حقول مشتقة واستثناءات مقصودة

القيمة | القرار | السبب
--- | --- | ---
ترتيب ونقاط الدوري | لا جدول تخزين؛ اشتقاق عند القراءة | تصحيح النتيجة ينعكس فورًا دون جمع مزدوج
TotalAmount | Quantity × UnitPrice عند القراءة | تجنب قيم مجموع غير متطابقة
الأهداف والتمريرات الإجمالية | تجميع إحصائيات المباريات المنتهية | عدم تكرار المجاميع القابلة للتغير
UnitPrice بالحجز | لقطة تاريخية مخزنة | حماية السعر المتفق عليه من تغير العرض
SoldQuantity | عداد مخزون مخزن مع Version | قفل وتحديث صف المخزون داخل معاملة الحجز
EventType | تمييز نوع مخزن | فرض التخصص الكلي والمنفصل

١٦ خطة التنفيذ والتحقق


## ١٦ خطة التنفيذ والتحقق

المسار المعتمد Code First

تبدأ التغييرات من Entities وArenaDbContext ثم يولد EF Migration وModelSnapshot. ينفذ setup-sqlserver.ps1 قاعدة SQL Server ويهيئ Admin اختياريًا من بيانات البيئة. لا توجد حاجة إلى Scaffold، لأن المصدر هنا النموذج البرمجي وليس قاعدة موجودة مسبقًا.

dotnet tool restore

dotnet ef migrations add ChangeName --project src/Arena.Infrastructure --startup-project src/Arena.Api

dotnet ef database update --project src/Arena.Infrastructure --startup-project src/Arena.Api

dotnet ef migrations script --idempotent --project src/Arena.Infrastructure --startup-project src/Arena.Api

dotnet ef migrations has-pending-model-changes --project src/Arena.Infrastructure --startup-project src/Arena.Api

ماذا يسلم من قاعدة البيانات

ملفات Migration وModelSnapshot، وسكربت 01_InitialCreate.sql القابل لإعادة التطبيق بحسب سجل migrations، وسكربت 02_VerifySchema.sql لفحص العلاقات والقيود، وschema.json بالتفاصيل الفعلية. يحتفظ المراجع بنسخة قاعدة احتياطية بعد تطبيق السكربت على SQL Server لدى الفريق.

التحقق المنجز وحدوده

تم بناء النموذج وتوليد Migration وتشغيل اختبارات قاعدة علائقية باستخدام SQLite. لم ينجح الاتصال بخادم SQL Server المحلي من جلسة التجهيز، ولذلك لا يمثل السكربت دليلًا على تطبيقه بالفعل على الخادم. يجب فحص اختلاف صياغة المزود والأقفال والـCollation والحجز المتزامن في SQL Server قبل النشر.

إذا اختير DB First مستقبلًا

ينشئ الفريق القاعدة أولًا، ثم يستخدم dotnet ef dbcontext scaffold مع مزود SQL Server ومجلد مستقل. لا يشغل Scaffold فوق الملفات الحالية باستخدام force دون مراجعة؛ ذلك قد يمحو قواعد Fluent Configuration. تغيير المسار قرار معماري للفريق وليس خطوة ناقصة في Code First.
