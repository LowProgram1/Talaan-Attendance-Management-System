using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AMIS.API;

public static class DemoDataFactory {
    private sealed record Person(string First, string Last, string? EmailOverride = null) {
        public string Email => EmailOverride ?? $"{Last.ToLowerInvariant()}.{First.ToLowerInvariant()}@gmail.com";
        public string FullName => $"{First} {Last}";
    }

    private static readonly Person[] Teachers = [
        new("Maria", "Santos"), new("Daniel", "Reyes"), new("Angela", "Cruz"),
        new("Rafael", "Mendoza"), new("Patricia", "Garcia"), new("Miguel", "Bautista"),
        new("Teresa", "Navarro"), new("Gabriel", "Castillo"), new("Lourdes", "Aquino"),
        new("Jose", "Villanueva"), new("Cecilia", "Ramos"), new("Andres", "Torres"),
        new("Elena", "Flores"), new("Roberto", "DelaCruz"), new("Nina", "Mercado"),
        new("Paolo", "Fernandez"), new("Isabel", "Salazar"), new("Marco", "Rivera"),
        new("Grace", "Domingo"), new("Antonio", "Valdez"), new("Carina", "Lopez"),
        new("Luis", "Soriano"), new("Beatriz", "Gonzales"), new("Emilio", "Pascual")
    ];

    private static readonly Person[] Guardians = [
        new("Jerald", "Kasim", "jerald.kasim@gmail.com"), new("Ana", "Villamor"), new("Jose", "Manalo"),
        new("Rosa", "Santiago"), new("Carlo", "Abad"), new("Mila", "Ocampo"),
        new("Renato", "Pineda"), new("Liza", "Soriano"), new("Nestor", "Tan"),
        new("Alma", "Lazaro"), new("Ramon", "Lim"), new("Joy", "Samson"),
        new("Edwin", "Dizon"), new("Miriam", "Uy"), new("Ernesto", "Manansala"),
        new("Sonia", "Serrano"), new("Felix", "Padilla"), new("Helen", "Go"),
        new("Oscar", "Legaspi"), new("Diana", "Ferrer"), new("Victor", "Aguilar"),
        new("Rowena", "Estrada"), new("Noel", "Aquino"), new("Leah", "Marcelo"),
        new("Arturo", "Enriquez"), new("Maribel", "David"), new("Rogelio", "Chua"),
        new("Lani", "Miranda"), new("Cesar", "Panganiban"), new("Nora", "Alonzo"),
        new("Felipe", "Sison"), new("Gloria", "Yap"), new("Danilo", "Rosales"),
        new("Carmen", "Guevarra"), new("Ricardo", "Belen"), new("Estrella", "Luna"),
        new("Manuel", "Macapagal"), new("Teresa", "Barrera"), new("Benjamin", "Soriano"),
        new("Amelia", "Concepcion"), new("Gerardo", "Tolentino"), new("Aurora", "Beltran"),
        new("Dominic", "Villareal"), new("Sylvia", "Cunanan"), new("Ruben", "Magsaysay"),
        new("Irene", "Espiritu"), new("Jaime", "Castaneda"), new("Corazon", "Alcantara"),
        new("Dante", "Laurel"), new("Marina", "Benedicto"), new("Armando", "Sarmiento"),
        new("Lourdes", "Lacsamana"), new("Pablo", "Quizon"), new("Rosario", "Capistrano"),
        new("Wilfredo", "Abella"), new("Evelyn", "Balmaceda"), new("Ramon", "Carpio"),
        new("Adela", "Magsino"), new("Teodoro", "Bautista"), new("Lourdes", "Ignacio")
    ];

    private static readonly (string Grade, string Department, string[] Sections)[] GradeDefinitions = [
        ("Grade 1", "Elementary", ["Rizal", "Bonifacio", "Mabini", "Del Pilar", "Luna", "Jacinto", "Silang"]),
        ("Grade 2", "Elementary", ["Sampaguita", "Gumamela", "Ilang-Ilang", "Rosal", "Santan", "Kalachuchi", "Waling-Waling"]),
        ("Grade 3", "Elementary", ["Narra", "Molave", "Acacia", "Mango", "Kamagong", "Yakal", "Talisay"]),
        ("Grade 4", "Elementary", ["Intramuros", "Vigan", "Mactan", "Barasoain", "Corregidor", "Banaue", "Kawit"]),
        ("Grade 5", "Elementary", ["Bayanihan", "Malasakit", "Paggalang", "Katapatan", "Sipag", "Disiplina", "Pagkakaisa"]),
        ("Grade 6", "Elementary", ["Luzon", "Visayas", "Mindanao", "Palawan", "Bohol", "Cebu", "Samar"])
    ];

    private static readonly string[] StudentFirstNames = [
        "Ariana", "Bianca", "Camille", "Daniel", "Enzo", "Faith", "Gabriel", "Hannah", "Isaac", "Jasmine",
        "Katrina", "Lorenzo", "Mikaela", "Nathan", "Olivia", "Paolo", "Queenie", "Rafael", "Sofia", "Tristan",
        "Una", "Vincent", "Wyatt", "Yasmin", "Zachary", "Amira", "Brandon", "Clarissa", "Dylan", "Elise",
        "Francis", "Gemma", "Harvey", "Ines", "Joaquin", "Kristine", "Leandro", "Mariel", "Noah", "Phoebe",
        "Rina", "Samuel", "Talia", "Uriel", "Veronica", "Wilfred", "Xandra", "Yuri", "Zia", "Adrian"
    ];

    private static readonly string[] StudentLastNames = [
        "Abad", "Alonzo", "Barrera", "Cabrera", "David", "Enriquez", "Fajardo", "Guevarra", "Hernandez", "Ignacio",
        "Jimenez", "Lazaro", "Manalo", "Navarro", "Ocampo", "Pascual", "Quinto", "Ramos", "Salazar", "Tolentino",
        "Umali", "Valencia", "Yap", "Zamora", "Aguilar", "Bautista", "Castillo", "Dizon", "Estrada", "Flores",
        "Garcia", "Luna", "Mendoza", "Reyes", "Santiago", "Torres", "Villanueva", "Soriano", "Domingo", "Mercado"
    ];

    public static async Task SeedAsync(AppDb db, UserManager<AppUser> users, IConfiguration configuration) {
        var teacherUsers = new List<AppUser>();
        foreach (var person in Teachers)
            teacherUsers.Add(await GetOrCreateUser(person, "Teacher", users, db, configuration, includeActivation: true));

        var guardianUsers = new List<AppUser>();
        foreach (var person in Guardians)
            guardianUsers.Add(await GetOrCreateUser(person, "Guardian", users, db, configuration, includeActivation: false));

        var studentCount = 0;
        var sectionCount = 0;
        var globalSectionIndex = 0;
        for (var gradeIndex = 0; gradeIndex < GradeDefinitions.Length; gradeIndex++) {
            var definition = GradeDefinitions[gradeIndex];
            var gradeRandom = new Random(20261013 + gradeIndex * 10_007);
            var grade = await db.GradeLevels.FirstOrDefaultAsync(g => g.Name == definition.Grade);
            if (grade is null) {
                grade = new GradeLevel { Name = definition.Grade, Department = definition.Department };
                db.GradeLevels.Add(grade);
                await db.SaveChangesAsync();
            }

            var sectionTotal = gradeRandom.Next(5, 8);
            for (var sectionIndex = 0; sectionIndex < sectionTotal; sectionIndex++) {
                var sectionName = definition.Sections[sectionIndex];
                var section = await db.Sections.FirstOrDefaultAsync(s => s.GradeLevelId == grade.Id && s.Name == sectionName);
                if (section is null) {
                    section = new Section {
                        Name = sectionName,
                        GradeLevelId = grade.Id,
                        Room = (101 + globalSectionIndex).ToString("D3"),
                        AdviserId = teacherUsers[globalSectionIndex % teacherUsers.Count].Id
                    };
                    db.Sections.Add(section);
                    await db.SaveChangesAsync();
                }
                sectionCount++;

                var studentRandom = new Random(20261013 + gradeIndex * 10_007 + (sectionIndex + 1) * 1_000_003);
                var studentTotal = studentRandom.Next(10, 16);
                var existingCount = await db.Students.CountAsync(s => s.SectionId == section.Id && !s.IsDeleted);
                var addedCount = 0;
                while (existingCount + addedCount < studentTotal) {
                    var firstName = StudentFirstNames[studentRandom.Next(StudentFirstNames.Length)];
                    var lastName = StudentLastNames[studentRandom.Next(StudentLastNames.Length)];
                    if (await db.Students.AnyAsync(s => s.SectionId == section.Id && s.FirstName == firstName && s.LastName == lastName)) continue;
                    var age = int.Parse(definition.Grade[^1..]) + 5;
                    var dob = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-age).AddDays(-studentRandom.Next(365));
                    db.Students.Add(new Student {
                        StudentNumber = await SchoolEndpoints.NextStudentNumber(db),
                        FirstName = firstName,
                        LastName = lastName,
                        DateOfBirth = dob,
                        SectionId = section.Id
                    });
                    await db.SaveChangesAsync();
                    addedCount++;
                    studentCount++;
                }

                var roster = await db.Students.Where(s => s.SectionId == section.Id && !s.IsDeleted).OrderBy(s => s.StudentNumber).ToListAsync();
                for (var studentIndex = 0; studentIndex < roster.Count; studentIndex++) {
                    var student = roster[studentIndex];
                    var guardian = definition.Grade == "Grade 1" && sectionIndex == 0 && studentIndex == 0
                        ? guardianUsers[0]
                        : guardianUsers[1 + ((gradeIndex * 97 + sectionIndex * 13 + studentIndex) % (guardianUsers.Count - 1))];
                    if (!await db.GuardianStudents.AnyAsync(x => x.GuardianId == guardian.Id && x.StudentId == student.Id)) {
                        db.GuardianStudents.Add(new GuardianStudent { GuardianId = guardian.Id, StudentId = student.Id });
                        await db.SaveChangesAsync();
                    }
                }
                globalSectionIndex++;
            }
        }

        await EnsureJeraldClassAsync(db, users, configuration);
        await EnsureEveryTeacherHasScheduleAsync(db, users);

        Console.WriteLine($"Demo data ready: {GradeDefinitions.Length} grades, {sectionCount} sections, {studentCount} new students, {teacherUsers.Count} teachers, {guardianUsers.Count} guardians.");
        Console.WriteLine($"Guardian alert test record: {guardianUsers[0].FullName} <{guardianUsers[0].Email}> is linked to a Grade 1 student.");
    }

    private static async Task EnsureJeraldClassAsync(AppDb db, UserManager<AppUser> users, IConfiguration configuration) {
        var teacher = await users.FindByEmailAsync("jerald.kasim@dict.gov.ph")
            ?? throw new InvalidOperationException("The teacher account jerald.kasim@dict.gov.ph is required before seeding the Grade 1 class.");
        if (!await users.IsInRoleAsync(teacher, "Teacher"))
            throw new InvalidOperationException("jerald.kasim@dict.gov.ph is not a Teacher account; no class records were changed.");

        var grade = await db.GradeLevels.FirstOrDefaultAsync(x => x.Name == "Grade 1")
            ?? throw new InvalidOperationException("Grade 1 must exist before seeding Jerald's class.");
        var section = await db.Sections.FirstOrDefaultAsync(x => x.GradeLevelId == grade.Id && x.Name == "Rizal" && x.Room == "101")
            ?? throw new InvalidOperationException("Grade 1 / Rizal room 101 must exist before seeding Jerald's class.");
        // The approved seed request explicitly assigns this demo section to Jerald,
        // replacing its earlier randomly assigned demo adviser.
        section.AdviserId = teacher.Id;

        var rosterCount = await db.Students.CountAsync(x => x.SectionId == section.Id && !x.IsDeleted);
        if (rosterCount > 20)
            throw new InvalidOperationException($"Grade 1 / Rizal already has {rosterCount} active students; refusing to delete students to reach 20.");

        var random = new Random(19910801);
        while (rosterCount < 20) {
            var firstName = StudentFirstNames[random.Next(StudentFirstNames.Length)];
            var lastName = StudentLastNames[random.Next(StudentLastNames.Length)];
            if (await db.Students.AnyAsync(x => x.SectionId == section.Id && !x.IsDeleted && x.FirstName == firstName && x.LastName == lastName)) continue;
            db.Students.Add(new Student {
                StudentNumber = await SchoolEndpoints.NextStudentNumber(db),
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-6).AddDays(-random.Next(365)),
                SectionId = section.Id
            });
            await db.SaveChangesAsync();
            rosterCount++;
        }

        var firstStudent = await db.Students.Where(x => x.SectionId == section.Id && !x.IsDeleted).OrderBy(x => x.StudentNumber).FirstAsync();

        var timetable = new (DayOfWeek Day, string Subject)[] {
            (DayOfWeek.Monday, "Science"), (DayOfWeek.Tuesday, "Math"),
            (DayOfWeek.Wednesday, "English"), (DayOfWeek.Thursday, "Filipino"),
            (DayOfWeek.Friday, "Values")
        };
        foreach (var (day, subject) in timetable) {
            var matching = await db.Schedules.Where(x => x.SectionId == section.Id && x.DayOfWeek == day && x.Subject == subject).ToListAsync();
            if (matching.Count > 1)
                throw new InvalidOperationException($"Duplicate {subject} schedules already exist for Grade 1 / Rizal on {day}.");
            if (matching.Count == 1) {
                var existing = matching[0];
                if (existing.TeacherId != teacher.Id || existing.StartsAt != new TimeOnly(8, 0) || existing.EndsAt != new TimeOnly(10, 0))
                    throw new InvalidOperationException($"The existing {subject} schedule for Grade 1 / Rizal on {day} differs from the approved timetable.");
                continue;
            }
            db.Schedules.Add(new Schedule {
                SectionId = section.Id, TeacherId = teacher.Id, Subject = subject,
                DayOfWeek = day, StartsAt = new TimeOnly(8, 0), EndsAt = new TimeOnly(10, 0)
            });
        }
        await db.SaveChangesAsync();
        var existingContact = await users.FindByEmailAsync("jerald11kasim@gmail.com");
        AppUser guardian;
        if (existingContact is null) {
            guardian = await GetOrCreateUser(new Person("Jerald", "Kasim", "jerald11kasim@gmail.com"), "Guardian", users, db, configuration, includeActivation: false);
        } else {
            var isGuardian = await users.IsInRoleAsync(existingContact, "Guardian");
            var isAdministrator = await users.IsInRoleAsync(existingContact, "Administrator");
            if (!isGuardian && !isAdministrator)
                throw new InvalidOperationException("jerald11kasim@gmail.com already belongs to a non-guardian, non-administrator account; its role was left unchanged.");
            if (!isGuardian) {
                var assigned = await users.AddToRoleAsync(existingContact, "Guardian");
                if (!assigned.Succeeded) throw new InvalidOperationException("Could not add guardian contact access while preserving administrator access: " + string.Join(" ", assigned.Errors.Select(x => x.Description)));
            }
            guardian = existingContact;
            if (guardian.FullName != "Jerald Kasim") {
                guardian.FullName = "Jerald Kasim";
                var update = await users.UpdateAsync(guardian);
                if (!update.Succeeded) throw new InvalidOperationException("Could not set the guardian contact name: " + string.Join(" ", update.Errors.Select(x => x.Description)));
            }
        }
        if (!await db.GuardianStudents.AnyAsync(x => x.GuardianId == guardian.Id && x.StudentId == firstStudent.Id))
            db.GuardianStudents.Add(new GuardianStudent { GuardianId = guardian.Id, StudentId = firstStudent.Id });
        await db.SaveChangesAsync();
        Console.WriteLine($"Jerald Kasim class ready: {rosterCount} students, guardian <{guardian.Email}>, 5 weekday schedules.");
    }

    private static async Task EnsureEveryTeacherHasScheduleAsync(AppDb db, UserManager<AppUser> users) {
        var teachers = (await users.GetUsersInRoleAsync("Teacher"))
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .ToList();
        var scheduledTeacherIds = await db.Schedules
            .Where(x => !x.IsArchived)
            .Select(x => x.TeacherId)
            .Distinct()
            .ToListAsync();
        var unscheduledTeachers = teachers.Where(x => !scheduledTeacherIds.Contains(x.Id)).ToList();
        if (unscheduledTeachers.Count == 0) {
            Console.WriteLine($"Teacher schedules ready: all {teachers.Count} active teachers already have an active schedule.");
            return;
        }

        var scheduledSectionIds = await db.Schedules
            .Where(x => !x.IsArchived && !x.Section.IsArchived && !x.Section.GradeLevel.IsArchived)
            .Select(x => x.SectionId)
            .Distinct()
            .ToListAsync();
        var availableSections = await db.Sections
            .Where(x => !x.IsArchived && !x.GradeLevel.IsArchived)
            .OrderBy(x => x.GradeLevel.Name)
            .ThenBy(x => x.Name)
            .ToListAsync();
        availableSections = availableSections.Where(x => !scheduledSectionIds.Contains(x.Id)).ToList();
        if (availableSections.Count < unscheduledTeachers.Count)
            throw new InvalidOperationException($"There are {unscheduledTeachers.Count} active teachers without schedules, but only {availableSections.Count} active sections without schedules.");

        var weekdays = new (DayOfWeek Day, string Subject)[] {
            (DayOfWeek.Monday, "Science"), (DayOfWeek.Tuesday, "Math"),
            (DayOfWeek.Wednesday, "English"), (DayOfWeek.Thursday, "Filipino"),
            (DayOfWeek.Friday, "Values")
        };
        for (var i = 0; i < unscheduledTeachers.Count; i++) {
            var (day, subject) = weekdays[i % weekdays.Length];
            db.Schedules.Add(new Schedule {
                SectionId = availableSections[i].Id,
                TeacherId = unscheduledTeachers[i].Id,
                Subject = subject,
                DayOfWeek = day,
                StartsAt = new TimeOnly(8, 0),
                EndsAt = new TimeOnly(10, 0)
            });
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"Teacher schedules added: {unscheduledTeachers.Count} active teachers assigned a 2-hour weekday class in an available section.");
    }

    private static async Task<AppUser> GetOrCreateUser(Person person, string role, UserManager<AppUser> users, AppDb db, IConfiguration configuration, bool includeActivation) {
        var user = await users.FindByEmailAsync(person.Email);
        if (user is null) {
            user = new AppUser { UserName = person.Email, Email = person.Email, FullName = person.FullName, IsActive = true, EmailConfirmed = false };
            var result = await users.CreateAsync(user);
            if (!result.Succeeded) throw new InvalidOperationException($"Could not create demo {role.ToLowerInvariant()} {person.Email}: {string.Join(" ", result.Errors.Select(x => x.Description))}");
            var assigned = await users.AddToRoleAsync(user, role);
            if (!assigned.Succeeded) throw new InvalidOperationException($"Could not assign {role} role to {person.Email}: {string.Join(" ", assigned.Errors.Select(x => x.Description))}");

        }

        if (!await users.IsInRoleAsync(user, role))
            throw new InvalidOperationException($"The existing account {person.Email} is not in the expected {role} role; it was left unchanged.");

        if (includeActivation && !user.EmailConfirmed) {
            var previousTokens = await db.OneTimeTokens.Where(x => x.UserId == user.Id && x.Purpose == "activation" && !x.Used).ToListAsync();
            foreach (var previous in previousTokens) previous.Used = true;
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            db.OneTimeTokens.Add(new OneTimeToken {
                UserId = user.Id,
                Purpose = "activation",
                Hash = AuthEndpoints.HashToken(token),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(2)
            });
            await db.SaveChangesAsync();
            var frontendUrl = string.IsNullOrWhiteSpace(configuration["PUBLIC_FRONTEND_URL"])
                ? configuration["FRONTEND_URL"] ?? "http://localhost:3000"
                : configuration["PUBLIC_FRONTEND_URL"]!;
            var link = $"{frontendUrl.TrimEnd('/')}/activate?email={Uri.EscapeDataString(person.Email)}&token={Uri.EscapeDataString(token)}";
            Console.WriteLine($"Teacher activation: {person.FullName} <{person.Email}> — {link}");
        }
        return user;
    }
}
