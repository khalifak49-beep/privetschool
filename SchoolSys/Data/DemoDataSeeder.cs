using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolSys.Models;
using SchoolSys.Security;

namespace SchoolSys.Data;

/// <summary>
/// يبني بيانات تجريبية واقعية: البنية الأكاديمية، المعلمون، الطلاب، أولياء الأمور،
/// الحضور، الاختبارات، الرسوم، والنقل المدرسي.
/// </summary>
public static class DemoDataSeeder
{
    private const int TargetStudents = 1250;
    private const int TargetTeachers = 86;
    private const int AttendanceDays = 10;

    private static readonly Random Rnd = new(20250816);

    /// <summary>
    /// بداية العام الدراسي محسوبة نسبةً لتاريخ اليوم، حتى تبقى البيانات التجريبية
    /// (الأقساط، المدفوعات، الحضور، الاختبارات) ذات معنى في أي وقت يُشغَّل فيه النظام.
    /// </summary>
    private static DateTime YearStart => DateTime.Today.Month >= 8
        ? new DateTime(DateTime.Today.Year, 8, 1)
        : new DateTime(DateTime.Today.Year - 1, 8, 1);

    private static DateTime YearEnd => YearStart.AddMonths(10).AddDays(-1);

    private static readonly string[] MaleNames =
    [
        "محمد", "أحمد", "عبدالله", "سالم", "خالد", "يوسف", "عمر", "علي", "حمد", "سيف",
        "ماجد", "طارق", "بدر", "ناصر", "فهد", "زياد", "راشد", "سعود", "مازن", "أنس",
        "إبراهيم", "حسن", "كريم", "وليد", "عادل", "رامي", "فارس", "غيث", "قيس", "نبيل"
    ];

    private static readonly string[] FemaleNames =
    [
        "فاطمة", "عائشة", "مريم", "نورة", "سارة", "هدى", "ليلى", "رنا", "أسماء", "زينب",
        "شيماء", "منى", "دانة", "رهف", "لمياء", "بشرى", "جواهر", "خديجة", "ريم", "سمية",
        "عبير", "غادة", "لطيفة", "مها", "نجلاء", "هند", "وفاء", "يسرا", "أمل", "بيان"
    ];

    private static readonly string[] FamilyNames =
    [
        "الحارثي", "البلوشي", "الشامسي", "العامري", "الكندي", "المعمري", "الرواحي", "الهنائي",
        "السيابي", "البوسعيدي", "الغافري", "الشعيلي", "الزدجالي", "المقبالي", "الفارسي", "الريامي",
        "الخروصي", "الوهيبي", "الصوافي", "النعماني", "الحجري", "البادي", "السالمي", "العبري",
        "الشحي", "الجابري", "المحروقي", "التوبي", "الرحبي", "الخليلي"
    ];

    private static readonly (string Code, string Name, int Weekly)[] SubjectDefs =
    [
        ("QRN", "القرآن الكريم", 3),
        ("ISL", "التربية الإسلامية", 3),
        ("ARB", "اللغة العربية", 6),
        ("ENG", "اللغة الإنجليزية", 5),
        ("MTH", "الرياضيات", 6),
        ("SCI", "العلوم", 4),
        ("PHY", "الفيزياء", 4),
        ("CHM", "الكيمياء", 4),
        ("BIO", "الأحياء", 4),
        ("SOC", "الدراسات الاجتماعية", 3),
        ("ICT", "تقنية المعلومات", 2),
        ("ART", "التربية الفنية", 2),
        ("PED", "التربية البدنية", 2),
        ("LFS", "المهارات الحياتية", 1)
    ];

    public static async Task SeedAsync(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        if (await db.Students.AnyAsync()) return;   // البيانات مزروعة مسبقاً

        var autoDetect = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            var year = await SeedAcademicYearAsync(db);
            var (stages, grades, sections) = await SeedStructureAsync(db, year);
            var subjects = await SeedSubjectsAsync(db, stages);
            var employees = await SeedEmployeesAsync(db);
            await SeedHomeroomTeachersAsync(db, sections, employees);
            var (teacherSubjects, _) = await SeedTeachingLoadAsync(db, year, sections, subjects, employees);
            await SeedTimetableAsync(db, year, teacherSubjects);
            var students = await SeedStudentsAsync(db, year, sections);
            await SeedGuardiansAsync(db, students);
            await SeedAttendanceAsync(db, year, students);
            await SeedExamsAndResultsAsync(db, year, teacherSubjects, students);
            await SeedHomeworkAsync(db, year, teacherSubjects, students);
            await SeedFinanceAsync(db, year, grades, students);
            await SeedTransportAsync(db, year, employees, students);
            await SeedAnnouncementsAsync(db);
            await SeedPortalUsersAsync(db, userManager, employees, students);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
        }
    }

    // ------------------------------------------------------------------
    private static async Task<AcademicYear> SeedAcademicYearAsync(ApplicationDbContext db)
    {
        var existing = await db.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent);
        if (existing is not null) return existing;

        var start = YearStart;
        var end = YearEnd;

        var year = new AcademicYear
        {
            Name = $"{start.Year} / {start.Year + 1}",
            StartDate = start,
            EndDate = end,
            IsCurrent = true
        };
        db.AcademicYears.Add(year);
        await db.SaveChangesAsync();

        var midPoint = start.AddMonths(5);
        var term1 = new Term
        {
            Name = "الفصل الدراسي الأول",
            SeqNo = 1,
            AcademicYearId = year.Id,
            StartDate = start,
            EndDate = midPoint.AddDays(-1)
        };
        var term2 = new Term
        {
            Name = "الفصل الدراسي الثاني",
            SeqNo = 2,
            AcademicYearId = year.Id,
            StartDate = midPoint,
            EndDate = end
        };

        // الفصل الحالي هو الذي يقع فيه تاريخ اليوم، وإلا فالفصل الأول
        var today = DateTime.Today;
        if (today >= term2.StartDate && today <= term2.EndDate) term2.IsCurrent = true;
        else term1.IsCurrent = true;

        db.Terms.AddRange(term1, term2);
        await db.SaveChangesAsync();
        return year;
    }

    private static async Task<(List<Stage>, List<Grade>, List<Section>)> SeedStructureAsync(
        ApplicationDbContext db, AcademicYear year)
    {
        var stageDefs = new (string Name, int Seq, string[] Grades, int SectionsPerGrade)[]
        {
            ("المرحلة الابتدائية", 1,
                ["الصف الأول", "الصف الثاني", "الصف الثالث", "الصف الرابع", "الصف الخامس", "الصف السادس"], 4),
            ("المرحلة المتوسطة", 2,
                ["الصف السابع", "الصف الثامن", "الصف التاسع"], 3),
            ("المرحلة الثانوية", 3,
                ["الصف العاشر", "الصف الحادي عشر", "الصف الثاني عشر"], 3)
        };

        var stages = new List<Stage>();
        foreach (var s in stageDefs)
            stages.Add(new Stage { Name = s.Name, SeqNo = s.Seq });
        db.Stages.AddRange(stages);
        await db.SaveChangesAsync();

        var grades = new List<Grade>();
        var gradeSeq = 1;
        for (var i = 0; i < stageDefs.Length; i++)
            foreach (var gName in stageDefs[i].Grades)
                grades.Add(new Grade { Name = gName, SeqNo = gradeSeq++, StageId = stages[i].Id });
        db.Grades.AddRange(grades);
        await db.SaveChangesAsync();

        // 42 شعبة: 6 صفوف × 4 + 6 صفوف × 3
        var sectionLetters = new[] { "أ", "ب", "ج", "د" };
        var sections = new List<Section>();
        var gi = 0;
        foreach (var sd in stageDefs)
        {
            foreach (var _ in sd.Grades)
            {
                var grade = grades[gi++];
                for (var k = 0; k < sd.SectionsPerGrade; k++)
                    sections.Add(new Section
                    {
                        Name = sectionLetters[k],
                        GradeId = grade.Id,
                        AcademicYearId = year.Id,
                        Capacity = 32,
                        Room = $"{grade.SeqNo}{k + 1:D2}"
                    });
            }
        }
        db.Sections.AddRange(sections);
        await db.SaveChangesAsync();

        return (stages, grades, sections);
    }

    private static async Task<List<Subject>> SeedSubjectsAsync(ApplicationDbContext db, List<Stage> stages)
    {
        var subjects = SubjectDefs.Select(s => new Subject
        {
            Code = s.Code,
            Name = s.Name,
            WeeklyPeriods = s.Weekly,
            MaxScore = 100,
            PassScore = 50
        }).ToList();

        // مواد المرحلة الثانوية فقط
        foreach (var code in new[] { "PHY", "CHM", "BIO" })
            subjects.First(s => s.Code == code).StageId = stages[2].Id;

        db.Subjects.AddRange(subjects);
        await db.SaveChangesAsync();
        return subjects;
    }

    private static async Task<List<Employee>> SeedEmployeesAsync(ApplicationDbContext db)
    {
        var employees = new List<Employee>();
        var seq = 1;

        Employee Make(EmployeeType type, string name, Gender g, string? spec = null) => new()
        {
            EmployeeNo = $"EMP{seq++:D4}",
            FullName = name,
            EmployeeType = type,
            Gender = g,
            Phone = RandomPhone(),
            Email = $"emp{seq:D4}@alnokhba.edu.om",
            BirthDate = new DateTime(Rnd.Next(1975, 1998), Rnd.Next(1, 13), Rnd.Next(1, 28)),
            HireDate = new DateTime(Rnd.Next(2012, 2025), Rnd.Next(1, 13), Rnd.Next(1, 28)),
            Specialization = spec,
            Qualification = Rnd.Next(10) < 7 ? "بكالوريوس" : "ماجستير",
            Salary = Rnd.Next(450, 1200) * 1m,
            NationalId = RandomNationalId()
        };

        // الإدارة
        employees.Add(Make(EmployeeType.Admin, "د. سعيد بن ناصر الحارثي", Gender.Male, "إدارة تربوية"));
        employees.Add(Make(EmployeeType.Admin, "أ. منى بنت خالد البوسعيدية", Gender.Female, "إدارة تربوية"));
        employees.Add(Make(EmployeeType.Admin, "أ. يوسف بن حمد الكندي", Gender.Male, "إشراف أكاديمي"));
        employees.Add(Make(EmployeeType.Accountant, "أ. عبدالله بن سالم الريامي", Gender.Male, "محاسبة"));
        employees.Add(Make(EmployeeType.Receptionist, "أ. هدى بنت علي الشعيلية", Gender.Female, "إدارة مكتبية"));
        employees.Add(Make(EmployeeType.Admin, "أ. ماجد بن راشد الغافري", Gender.Male, "إدارة النقل"));

        // المعلمون
        var specs = SubjectDefs.Select(s => s.Name).ToArray();
        for (var i = 0; i < TargetTeachers; i++)
        {
            var isMale = i % 2 == 0;
            var name = isMale
                ? $"{Pick(MaleNames)} بن {Pick(MaleNames)} {Pick(FamilyNames)}"
                : $"{Pick(FemaleNames)} بنت {Pick(MaleNames)} {Pick(FamilyNames)}";
            employees.Add(Make(EmployeeType.Teacher, name, isMale ? Gender.Male : Gender.Female, specs[i % specs.Length]));
        }

        // سائقون ومشرفون
        for (var i = 0; i < 6; i++)
            employees.Add(Make(EmployeeType.Driver, $"{Pick(MaleNames)} بن {Pick(MaleNames)} {Pick(FamilyNames)}", Gender.Male, "قيادة حافلات"));
        for (var i = 0; i < 6; i++)
            employees.Add(Make(EmployeeType.BusSupervisor, $"{Pick(FemaleNames)} بنت {Pick(MaleNames)} {Pick(FamilyNames)}", Gender.Female, "إشراف نقل"));

        db.Employees.AddRange(employees);
        await db.SaveChangesAsync();
        return employees;
    }

    private static async Task SeedHomeroomTeachersAsync(
        ApplicationDbContext db, List<Section> sections, List<Employee> employees)
    {
        var teachers = employees.Where(e => e.EmployeeType == EmployeeType.Teacher).ToList();
        for (var i = 0; i < sections.Count; i++)
            sections[i].HomeroomTeacherId = teachers[i % teachers.Count].Id;

        db.Sections.UpdateRange(sections);
        await db.SaveChangesAsync();
    }

    private static async Task<(List<TeacherSubject>, List<Subject>)> SeedTeachingLoadAsync(
        ApplicationDbContext db, AcademicYear year, List<Section> sections,
        List<Subject> subjects, List<Employee> employees)
    {
        var teachers = employees.Where(e => e.EmployeeType == EmployeeType.Teacher).ToList();
        var core = subjects.Where(s => s.StageId == null).ToList();       // مواد عامة
        var secondaryOnly = subjects.Where(s => s.StageId != null).ToList();

        var gradesById = await db.Grades.ToDictionaryAsync(g => g.Id);
        var stageIds = await db.Stages.OrderBy(s => s.SeqNo).Select(s => s.Id).ToListAsync();
        var secondaryStageId = stageIds[2];

        var load = new List<TeacherSubject>();
        var t = 0;
        foreach (var section in sections)
        {
            var isSecondary = gradesById[section.GradeId].StageId == secondaryStageId;
            var sectionSubjects = isSecondary
                ? core.Where(s => s.Code is "ISL" or "ARB" or "ENG" or "MTH" or "ICT" or "PED")
                      .Concat(secondaryOnly).ToList()
                : core.Where(s => s.Code is "QRN" or "ISL" or "ARB" or "ENG" or "MTH" or "SCI" or "SOC" or "ART" or "PED")
                      .ToList();

            foreach (var subject in sectionSubjects)
            {
                load.Add(new TeacherSubject
                {
                    TeacherId = teachers[t++ % teachers.Count].Id,
                    SubjectId = subject.Id,
                    SectionId = section.Id,
                    AcademicYearId = year.Id
                });
            }
        }

        db.TeacherSubjects.AddRange(load);
        await db.SaveChangesAsync();
        return (load, subjects);
    }

    private static async Task SeedTimetableAsync(
        ApplicationDbContext db, AcademicYear year, List<TeacherSubject> load)
    {
        // 5 أيام (الأحد - الخميس) × 6 حصص
        var periodTimes = new (TimeSpan Start, TimeSpan End)[]
        {
            (new(7, 30, 0), new(8, 15, 0)),
            (new(8, 15, 0), new(9, 0, 0)),
            (new(9, 20, 0), new(10, 5, 0)),
            (new(10, 5, 0), new(10, 50, 0)),
            (new(11, 10, 0), new(11, 55, 0)),
            (new(11, 55, 0), new(12, 40, 0))
        };

        var slots = new List<TimetableSlot>();
        foreach (var group in load.GroupBy(l => l.SectionId))
        {
            var items = group.ToList();
            var idx = 0;
            for (var day = 0; day < 5; day++)
                for (var period = 0; period < 6; period++)
                {
                    var ts = items[idx++ % items.Count];
                    slots.Add(new TimetableSlot
                    {
                        SectionId = group.Key,
                        SubjectId = ts.SubjectId,
                        TeacherId = ts.TeacherId,
                        AcademicYearId = year.Id,
                        DayOfWeek = day,
                        PeriodNo = period + 1,
                        StartTime = periodTimes[period].Start,
                        EndTime = periodTimes[period].End
                    });
                }
        }

        db.TimetableSlots.AddRange(slots);
        await db.SaveChangesAsync();
    }

    private static async Task<List<Student>> SeedStudentsAsync(
        ApplicationDbContext db, AcademicYear year, List<Section> sections)
    {
        var students = new List<Student>(TargetStudents);
        var perSection = TargetStudents / sections.Count;      // ~29
        var remainder = TargetStudents % sections.Count;
        var no = 1;

        foreach (var section in sections)
        {
            var count = perSection + (remainder-- > 0 ? 1 : 0);
            for (var i = 0; i < count; i++)
            {
                var isMale = Rnd.Next(2) == 0;
                var first = isMale ? Pick(MaleNames) : Pick(FemaleNames);
                var father = Pick(MaleNames);
                var family = Pick(FamilyNames);

                students.Add(new Student
                {
                    StudentNo = $"{YearStart.Year}{no++:D4}",
                    FullName = $"{first} {father} {family}",
                    Gender = isMale ? Gender.Male : Gender.Female,
                    BirthDate = DateTime.Today.AddYears(-(6 + sections.IndexOf(section) / 4)).AddDays(-Rnd.Next(0, 365)),
                    Nationality = "عُماني",
                    Religion = "الإسلام",
                    BirthPlace = "مسقط",
                    Address = $"ولاية {Pick(["السيب", "بوشر", "مطرح", "العامرات", "قريات"])} - مسقط",
                    Phone = RandomPhone(),
                    NationalId = RandomNationalId(),
                    CurrentSectionId = section.Id,
                    EnrollmentDate = YearStart.AddDays(Rnd.Next(0, 14)),
                    Status = StudentStatus.Active,
                    BloodType = Pick(["A+", "B+", "O+", "AB+", "A-", "O-"])
                });
            }
        }

        db.Students.AddRange(students);
        await db.SaveChangesAsync();

        var enrollments = students.Select(s => new Enrollment
        {
            StudentId = s.Id,
            SectionId = s.CurrentSectionId!.Value,
            AcademicYearId = year.Id,
            EnrollDate = s.EnrollmentDate,
            IsActive = true
        }).ToList();
        db.Enrollments.AddRange(enrollments);
        await db.SaveChangesAsync();

        return students;
    }

    private static async Task SeedGuardiansAsync(ApplicationDbContext db, List<Student> students)
    {
        var guardians = new List<Guardian>();
        var links = new List<StudentGuardian>();

        // بعض أولياء الأمور لديهم أكثر من ابن (إخوة)
        var i = 0;
        while (i < students.Count)
        {
            var siblings = Rnd.Next(10) < 3 ? Math.Min(2, students.Count - i) : 1;
            var family = students[i].FullName.Split(' ').Last();
            var guardian = new Guardian
            {
                FullName = $"{students[i].FullName.Split(' ')[1]} {Pick(MaleNames)} {family}",
                Phone = RandomPhone(),
                AltPhone = Rnd.Next(2) == 0 ? RandomPhone() : null,
                NationalId = RandomNationalId(),
                Job = Pick(["موظف حكومي", "مهندس", "طبيب", "معلم", "تاجر", "عسكري", "أعمال حرة"]),
                Workplace = Pick(["وزارة التربية والتعليم", "شركة تنمية نفط عمان", "مستشفى السلطان قابوس", "القطاع الخاص"]),
                Address = students[i].Address,
                Email = null
            };
            guardians.Add(guardian);

            for (var k = 0; k < siblings; k++)
                links.Add(new StudentGuardian
                {
                    Student = students[i + k],
                    Guardian = guardian,
                    Relation = "الأب",
                    IsPrimary = true,
                    CanPickup = true
                });

            i += siblings;
        }

        db.Guardians.AddRange(guardians);
        db.StudentGuardians.AddRange(links);
        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync();
    }

    private static async Task SeedAttendanceAsync(
        ApplicationDbContext db, AcademicYear year, List<Student> students)
    {
        var days = new List<DateTime>();
        var d = DateTime.Today;
        while (days.Count < AttendanceDays)
        {
            if (d.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday))
                days.Add(d);
            d = d.AddDays(-1);
        }

        var records = new List<StudentAttendance>(students.Count * days.Count);
        foreach (var day in days)
            foreach (var s in students)
            {
                var roll = Rnd.Next(100);
                var status = roll switch
                {
                    < 92 => AttendanceStatus.Present,
                    < 96 => AttendanceStatus.Late,
                    < 98 => AttendanceStatus.Excused,
                    _ => AttendanceStatus.Absent
                };

                records.Add(new StudentAttendance
                {
                    StudentId = s.Id,
                    SectionId = s.CurrentSectionId!.Value,
                    AcademicYearId = year.Id,
                    Date = day,
                    Status = status,
                    CheckInTime = status is AttendanceStatus.Present or AttendanceStatus.Late
                        ? new TimeSpan(7, Rnd.Next(5, 40), 0)
                        : null,
                    LateMinutes = status == AttendanceStatus.Late ? Rnd.Next(5, 35) : 0,
                    Method = Rnd.Next(2) == 0 ? AttendanceMethod.QrCode : AttendanceMethod.Manual,
                    GuardianNotified = status == AttendanceStatus.Absent
                });
            }

        await BulkInsertAsync(db, records);
    }

    private static async Task SeedExamsAndResultsAsync(
        ApplicationDbContext db, AcademicYear year, List<TeacherSubject> load, List<Student> students)
    {
        var term = await db.Terms.FirstAsync(t => t.IsCurrent);
        var studentsBySection = students.GroupBy(s => s.CurrentSectionId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var exams = new List<Exam>();
        foreach (var group in load.GroupBy(l => l.SectionId))
        {
            // اختباران لكل شعبة على مادتين أساسيتين
            foreach (var ts in group.Take(3))
            {
                exams.Add(new Exam
                {
                    Title = "اختبار الفترة الأولى",
                    ExamType = ExamType.Monthly,
                    SubjectId = ts.SubjectId,
                    SectionId = ts.SectionId,
                    TermId = term.Id,
                    ExamDate = DateTime.Today.AddDays(-Rnd.Next(5, 30)),
                    StartTime = new TimeSpan(8, 15, 0),
                    DurationMinutes = 60,
                    MaxScore = 20,
                    PassScore = 10,
                    Weight = 20,
                    Status = ExamStatus.Graded
                });
            }
        }
        db.Exams.AddRange(exams);
        await db.SaveChangesAsync();

        var results = new List<ExamResult>();
        foreach (var exam in exams)
        {
            if (!studentsBySection.TryGetValue(exam.SectionId, out var list)) continue;
            foreach (var s in list)
            {
                var absent = Rnd.Next(100) < 2;
                results.Add(new ExamResult
                {
                    ExamId = exam.Id,
                    StudentId = s.Id,
                    IsAbsent = absent,
                    Score = absent ? null : Math.Round((decimal)(Rnd.NextDouble() * 0.45 + 0.55) * exam.MaxScore, 2),
                    EnteredAt = exam.ExamDate.AddDays(2)
                });
            }
        }

        await BulkInsertAsync(db, results);

        // اختبارات قادمة للوحة التحكم
        var upcoming = load.GroupBy(l => l.SectionId).Take(8).Select((g, i) =>
        {
            var ts = g.First();
            return new Exam
            {
                Title = "اختبار الفترة الثانية",
                ExamType = ExamType.Midterm,
                SubjectId = ts.SubjectId,
                SectionId = ts.SectionId,
                TermId = term.Id,
                ExamDate = DateTime.Today.AddDays(i + 2),
                StartTime = new TimeSpan(8, 15, 0),
                DurationMinutes = 90,
                MaxScore = 30,
                PassScore = 15,
                Weight = 30,
                Status = ExamStatus.Published
            };
        }).ToList();
        db.Exams.AddRange(upcoming);
        await db.SaveChangesAsync();
    }

    private static async Task SeedHomeworkAsync(
        ApplicationDbContext db, AcademicYear year, List<TeacherSubject> load, List<Student> students)
    {
        var term = await db.Terms.FirstAsync(t => t.IsCurrent);
        var homeworks = new List<Homework>();

        foreach (var group in load.GroupBy(l => l.SectionId).Take(20))
        {
            var ts = group.First();
            homeworks.Add(new Homework
            {
                Title = "واجب الوحدة الثالثة",
                Description = "حل التمارين من صفحة 45 إلى 52 مع كتابة الملخص.",
                SubjectId = ts.SubjectId,
                SectionId = ts.SectionId,
                TeacherId = ts.TeacherId,
                TermId = term.Id,
                AssignedDate = DateTime.Today.AddDays(-3),
                DueDate = DateTime.Today.AddDays(2),
                MaxScore = 10,
                IsPublished = true
            });
        }

        db.Homeworks.AddRange(homeworks);
        await db.SaveChangesAsync();

        var studentsBySection = students.GroupBy(s => s.CurrentSectionId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var submissions = new List<HomeworkSubmission>();
        foreach (var hw in homeworks)
        {
            if (!studentsBySection.TryGetValue(hw.SectionId, out var list)) continue;
            foreach (var s in list)
            {
                var roll = Rnd.Next(100);
                submissions.Add(new HomeworkSubmission
                {
                    HomeworkId = hw.Id,
                    StudentId = s.Id,
                    Status = roll < 60 ? HomeworkStatus.Submitted
                        : roll < 75 ? HomeworkStatus.Graded
                        : roll < 85 ? HomeworkStatus.Late
                        : HomeworkStatus.NotSubmitted,
                    SubmittedAt = roll < 85 ? DateTime.Now.AddDays(-Rnd.Next(0, 3)) : null,
                    Score = roll is >= 60 and < 75 ? Rnd.Next(6, 11) : null
                });
            }
        }

        await BulkInsertAsync(db, submissions);
    }

    private static async Task SeedFinanceAsync(
        ApplicationDbContext db, AcademicYear year, List<Grade> grades, List<Student> students)
    {
        var feeItems = new List<FeeItem>
        {
            new() { Name = "الرسوم الدراسية", DefaultAmount = 1200, AcademicYearId = year.Id, IsMandatory = true },
            new() { Name = "رسوم الكتب والمقررات", DefaultAmount = 120, AcademicYearId = year.Id, IsMandatory = true },
            new() { Name = "رسوم الزي المدرسي", DefaultAmount = 80, AcademicYearId = year.Id, IsMandatory = false },
            new() { Name = "رسوم الأنشطة والرحلات", DefaultAmount = 60, AcademicYearId = year.Id, IsMandatory = false },
            new() { Name = "رسوم التسجيل", DefaultAmount = 50, AcademicYearId = year.Id, IsMandatory = true }
        };
        db.FeeItems.AddRange(feeItems);
        await db.SaveChangesAsync();

        var invoices = new List<Invoice>();
        var seq = 1;
        foreach (var s in students)
        {
            var lines = feeItems.Where(f => f.IsMandatory || Rnd.Next(2) == 0).ToList();
            var total = lines.Sum(l => l.DefaultAmount);
            var discount = Rnd.Next(100) < 12 ? Math.Round(total * 0.10m, 2) : 0m;
            var net = total - discount;

            invoices.Add(new Invoice
            {
                InvoiceNo = $"INV-{YearStart.Year}-{seq++:D5}",
                StudentId = s.Id,
                AcademicYearId = year.Id,
                IssueDate = YearStart,
                TotalAmount = total,
                DiscountAmount = discount,
                NetAmount = net,
                PaidAmount = 0,
                Status = InvoiceStatus.Unpaid,
                Lines = lines.Select(l => new InvoiceLine
                {
                    FeeItemId = l.Id,
                    Description = l.Name,
                    Amount = l.DefaultAmount
                }).ToList(),
                Installments = BuildInstallments(net)
            });
        }

        db.ChangeTracker.DetectChanges();
        await BulkInsertAsync(db, invoices);

        // تسجيل مدفوعات لجزء من الفواتير
        var payments = new List<Payment>();
        var receiptSeq = 1;
        var allInstallments = await db.Installments
            .Where(i => invoices.Select(x => x.Id).Contains(i.InvoiceId))
            .ToListAsync();
        var byInvoice = allInstallments.GroupBy(i => i.InvoiceId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.SeqNo).ToList());

        foreach (var inv in invoices)
        {
            if (!byInvoice.TryGetValue(inv.Id, out var insts)) continue;

            var today = DateTime.Today;
            var duePassed = insts.Where(i => i.DueDate <= today).ToList();
            var roll = Rnd.Next(100);

            // أنماط سداد واقعية: مسدِّد مقدماً / منتظم / متأخر بقسط / متعثر
            var toPay = roll switch
            {
                < 15 => insts,
                < 55 => duePassed,
                < 82 => duePassed.Take(Math.Max(0, duePassed.Count - 1)).ToList(),
                _ => []
            };

            foreach (var inst in toPay)
            {
                inst.PaidAmount = inst.Amount;
                inst.Status = InstallmentStatus.Paid;
                inv.PaidAmount += inst.Amount;

                var payDate = inst.DueDate.AddDays(Rnd.Next(-6, 7));
                if (payDate > today) payDate = today.AddDays(-Rnd.Next(0, 25));
                if (payDate < YearStart) payDate = YearStart;

                payments.Add(new Payment
                {
                    ReceiptNo = $"RCP-{YearStart.Year}-{receiptSeq++:D5}",
                    InvoiceId = inv.Id,
                    InstallmentId = inst.Id,
                    StudentId = inv.StudentId,
                    Amount = inst.Amount,
                    PaymentDate = payDate,
                    Method = (PaymentMethod)Rnd.Next(1, 6),
                    Reference = null
                });
            }

            foreach (var inst in insts.Where(x => x.PaidAmount < x.Amount && x.DueDate < today))
                inst.Status = InstallmentStatus.Overdue;

            inv.Status = inv.PaidAmount <= 0 ? InvoiceStatus.Unpaid
                : inv.PaidAmount >= inv.NetAmount ? InvoiceStatus.Paid
                : InvoiceStatus.PartiallyPaid;
        }

        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync();
        await BulkInsertAsync(db, payments);
    }

    private static List<Installment> BuildInstallments(decimal net)
    {
        var count = 4;
        var each = Math.Round(net / count, 2);
        var list = new List<Installment>();

        // أقساط موزّعة على العام الدراسي: بعضها مستحق سابقاً وبعضها لاحقاً
        var dueDates = new[]
        {
            YearStart.AddDays(14), YearStart.AddMonths(3),
            YearStart.AddMonths(5), YearStart.AddMonths(7)
        };

        for (var i = 0; i < count; i++)
        {
            var amount = i == count - 1 ? net - each * (count - 1) : each;
            list.Add(new Installment
            {
                SeqNo = i + 1,
                Name = $"القسط {i + 1}",
                DueDate = dueDates[i],
                Amount = amount,
                Status = InstallmentStatus.Pending
            });
        }
        return list;
    }

    private static async Task SeedTransportAsync(
        ApplicationDbContext db, AcademicYear year, List<Employee> employees, List<Student> students)
    {
        var drivers = employees.Where(e => e.EmployeeType == EmployeeType.Driver).ToList();
        var supervisors = employees.Where(e => e.EmployeeType == EmployeeType.BusSupervisor).ToList();

        var buses = new List<Bus>();
        for (var i = 0; i < 6; i++)
            buses.Add(new Bus
            {
                BusNo = $"BUS-{i + 1:D2}",
                PlateNo = $"{Rnd.Next(10000, 99999)} أ",
                Capacity = 30,
                Model = Pick(["Toyota Coaster", "Mitsubishi Rosa", "Hyundai County"]),
                ManufactureYear = Rnd.Next(2016, 2024),
                DriverId = drivers[i].Id,
                SupervisorId = supervisors[i].Id,
                LicenseExpiry = DateTime.Today.AddMonths(Rnd.Next(2, 20))
            });
        db.Buses.AddRange(buses);
        await db.SaveChangesAsync();

        var areas = new[] { "السيب", "بوشر", "مطرح", "العامرات", "قريات", "الخوض" };
        var routes = new List<TransportRoute>();
        for (var i = 0; i < 6; i++)
            routes.Add(new TransportRoute
            {
                Code = $"R-{i + 1:D2}",
                Name = $"خط {areas[i]}",
                BusId = buses[i].Id,
                MonthlyFee = 25,
                Description = $"يخدم منطقة {areas[i]} والمناطق المجاورة"
            });
        db.TransportRoutes.AddRange(routes);
        await db.SaveChangesAsync();

        var stops = new List<RouteStop>();
        foreach (var route in routes)
            for (var k = 0; k < 5; k++)
                stops.Add(new RouteStop
                {
                    RouteId = route.Id,
                    Name = $"محطة {route.Name.Replace("خط ", "")} {k + 1}",
                    SeqNo = k + 1,
                    PickupTime = new TimeSpan(6, 15 + k * 8, 0),
                    DropTime = new TimeSpan(13, 45 + k * 8, 0)
                });
        db.RouteStops.AddRange(stops);
        await db.SaveChangesAsync();

        // اشتراك حوالي 30% من الطلاب
        var stopsByRoute = stops.GroupBy(s => s.RouteId).ToDictionary(g => g.Key, g => g.ToList());
        var subs = new List<StudentTransport>();
        foreach (var s in students.Where(_ => Rnd.Next(100) < 30))
        {
            var route = routes[Rnd.Next(routes.Count)];
            subs.Add(new StudentTransport
            {
                StudentId = s.Id,
                RouteId = route.Id,
                StopId = stopsByRoute[route.Id][Rnd.Next(5)].Id,
                AcademicYearId = year.Id,
                MonthlyFee = route.MonthlyFee,
                StartDate = YearStart,
                IsActive = true
            });
        }
        await BulkInsertAsync(db, subs);
    }

    private static async Task SeedAnnouncementsAsync(ApplicationDbContext db)
    {
        db.Announcements.AddRange(
            new Announcement
            {
                Title = "بدء اختبارات الفترة الثانية",
                Body = "تبدأ اختبارات الفترة الثانية يوم الأحد القادم وفق الجدول المعلن. نتمنى للجميع التوفيق.",
                Audience = AnnouncementAudience.All,
                PublishDate = DateTime.Today.AddDays(-1),
                ExpiryDate = DateTime.Today.AddDays(20),
                IsPinned = true
            },
            new Announcement
            {
                Title = "اجتماع أولياء الأمور",
                Body = "يسر إدارة المدرسة دعوتكم لحضور اللقاء التربوي يوم الخميس الساعة 5 مساءً في قاعة المدرسة.",
                Audience = AnnouncementAudience.Guardians,
                PublishDate = DateTime.Today.AddDays(-3),
                ExpiryDate = DateTime.Today.AddDays(10)
            },
            new Announcement
            {
                Title = "تذكير بسداد القسط الثالث",
                Body = "نذكّر أولياء الأمور بموعد استحقاق القسط الثالث. يرجى المراجعة مع قسم المحاسبة.",
                Audience = AnnouncementAudience.Guardians,
                PublishDate = DateTime.Today.AddDays(-5),
                ExpiryDate = DateTime.Today.AddDays(15)
            },
            new Announcement
            {
                Title = "ورشة تدريبية للمعلمين",
                Body = "ورشة بعنوان (استراتيجيات التعلم النشط) يوم الأربعاء في مركز مصادر التعلم.",
                Audience = AnnouncementAudience.Teachers,
                PublishDate = DateTime.Today.AddDays(-2),
                ExpiryDate = DateTime.Today.AddDays(7)
            }
        );
        await db.SaveChangesAsync();
    }

    /// <summary>ينشئ حسابات دخول تجريبية لكل دور.</summary>
    private static async Task SeedPortalUsersAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        List<Employee> employees, List<Student> students)
    {
        var principal = employees[0];
        var vice = employees[1];
        var academic = employees[2];
        var accountant = employees[3];
        var reception = employees[4];
        var transport = employees[5];
        var teacher = employees.First(e => e.EmployeeType == EmployeeType.Teacher);

        await CreateAsync(userManager, "principal@school.local", principal.FullName, RoleNames.Principal, employeeId: principal.Id);
        await CreateAsync(userManager, "vice@school.local", vice.FullName, RoleNames.VicePrincipal, employeeId: vice.Id);
        await CreateAsync(userManager, "academic@school.local", academic.FullName, RoleNames.AcademicAdmin, employeeId: academic.Id);
        await CreateAsync(userManager, "accountant@school.local", accountant.FullName, RoleNames.Accountant, employeeId: accountant.Id);
        await CreateAsync(userManager, "reception@school.local", reception.FullName, RoleNames.Receptionist, employeeId: reception.Id);
        await CreateAsync(userManager, "transport@school.local", transport.FullName, RoleNames.TransportManager, employeeId: transport.Id);
        await CreateAsync(userManager, "teacher@school.local", teacher.FullName, RoleNames.Teacher, employeeId: teacher.Id);

        var student = students[0];
        await CreateAsync(userManager, "student@school.local", student.FullName, RoleNames.Student, studentId: student.Id);

        var guardianLink = await db.StudentGuardians
            .Include(sg => sg.Guardian)
            .FirstOrDefaultAsync(sg => sg.StudentId == student.Id);
        if (guardianLink is not null)
            await CreateAsync(userManager, "guardian@school.local", guardianLink.Guardian.FullName,
                RoleNames.Guardian, guardianId: guardianLink.GuardianId);
    }

    private static async Task CreateAsync(
        UserManager<ApplicationUser> userManager, string email, string fullName, string role,
        int? employeeId = null, int? studentId = null, int? guardianId = null)
    {
        if (await userManager.FindByEmailAsync(email) is not null) return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            IsActive = true,
            EmployeeId = employeeId,
            StudentId = studentId,
            GuardianId = guardianId
        };

        var result = await userManager.CreateAsync(user, DbSeeder.DefaultPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, role);
    }

    // ------------------------------------------------------------------
    private static async Task BulkInsertAsync<T>(ApplicationDbContext db, List<T> items) where T : class
    {
        const int batchSize = 2000;
        for (var i = 0; i < items.Count; i += batchSize)
        {
            db.Set<T>().AddRange(items.Skip(i).Take(batchSize));
            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync();
        }
    }

    private static string Pick(string[] values) => values[Rnd.Next(values.Length)];
    private static string RandomPhone() => $"9{Rnd.Next(1000000, 9999999)}";
    private static string RandomNationalId() => Rnd.Next(10000000, 99999999).ToString();
}
