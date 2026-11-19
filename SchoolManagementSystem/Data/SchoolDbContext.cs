using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data.Entities;

public class SchoolDbContext : IdentityDbContext<User>
{
    // Change this line if you must maintain a separate collection declaration
    public new DbSet<User> Users { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<SchoolClass> SchoolClasses { get; set; }
    public DbSet<Teacher> Teachers { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<Grade> Grades { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Alert> Alerts { get; set; }
    public DbSet<TeacherSubject> TeacherSubjects { get; set; }
    public DbSet<TeacherSchoolClass> TeacherSchoolClasses { get; set; }
    public DbSet<CourseSubject> CourseSubjects { get; set; }
    


    public SchoolDbContext(DbContextOptions<SchoolDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //// Relacionamentos para SchoolClass
        //modelBuilder.Entity<Subject>()
        //    .HasOne(s => s.SchoolClass)
        //    .WithMany(sc => sc.Subjects)
        //    .HasForeignKey(s => s.SchoolClassId)
        //    .OnDelete(DeleteBehavior.Restrict); // Restringe a exclusão

        modelBuilder.Entity<Student>()
            .HasOne(s => s.SchoolClass)
            .WithMany(sc => sc.Students)
            .HasForeignKey(s => s.SchoolClassId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship between Student and User
        modelBuilder.Entity<Student>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Many-to-many relationship between Teacher and Subject
        modelBuilder.Entity<TeacherSubject>()
            .HasKey(ts => new { ts.TeacherId, ts.SubjectId }); // Composite key

        modelBuilder.Entity<TeacherSubject>()
            .HasOne(ts => ts.Teacher)
            .WithMany(t => t.TeacherSubjects)
            .HasForeignKey(ts => ts.TeacherId)
            .OnDelete(DeleteBehavior.Cascade); // Automatically deletes associations when deleting a Teacher

        modelBuilder.Entity<TeacherSubject>()
            .HasOne(ts => ts.Subject)
            .WithMany(s => s.TeacherSubjects)
            .HasForeignKey(ts => ts.SubjectId)
            .OnDelete(DeleteBehavior.Restrict); // Restriction on exclusion from the subject


        // Configura o relacionamento muitos-para-muitos
        modelBuilder.Entity<CourseSubject>()
            .HasKey(cs => new { cs.CourseId, cs.SubjectId });

        modelBuilder.Entity<CourseSubject>()
            .HasOne(cs => cs.Course)
            .WithMany(c => c.CourseSubjects)
            .HasForeignKey(cs => cs.CourseId)
            .OnDelete(DeleteBehavior.Cascade); // Permitir exclusão em cascata

        modelBuilder.Entity<CourseSubject>()
            .HasOne(cs => cs.Subject)
            .WithMany(s => s.CourseSubjects)
            .HasForeignKey(cs => cs.SubjectId)
            .OnDelete(DeleteBehavior.Cascade); // Permitir exclusão em cascata

        modelBuilder.Entity<SchoolClass>()
            .HasOne(sc => sc.Course)
            .WithMany(c => c.SchoolClasses)
            .HasForeignKey(sc => sc.CourseId)
            .OnDelete(DeleteBehavior.SetNull); // Defina como nulo em vez de excluir


        // Relationship between Teacher and SchoolClass
        modelBuilder.Entity<TeacherSchoolClass>()
            .HasKey(tsc => new { tsc.TeacherId, tsc.SchoolClassId }); // Composite key

        modelBuilder.Entity<TeacherSchoolClass>()
            .HasOne(tsc => tsc.Teacher)
            .WithMany(t => t.TeacherSchoolClasses)
            .HasForeignKey(tsc => tsc.TeacherId)
            .OnDelete(DeleteBehavior.Cascade); // Automatically deletes associations when deleting a Teacher

        modelBuilder.Entity<TeacherSchoolClass>()
            .HasOne(tsc => tsc.SchoolClass)
            .WithMany(sc => sc.TeacherSchoolClasses)
            .HasForeignKey(tsc => tsc.SchoolClassId)
            .OnDelete(DeleteBehavior.Restrict); // Restriction on class exclusion
    }
        // 💡 INTERCEPTOR WORKAROUND: Catches missing form fields right before saving to SQL Server
        public override System.Threading.Tasks.Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess, 
            System.Threading.CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is SchoolManagementSystem.Data.Entities.User && 
                           (e.State == Microsoft.EntityFrameworkCore.EntityState.Added || 
                            e.State == Microsoft.EntityFrameworkCore.EntityState.Modified));

            foreach (var entry in entries)
            {
                var user = (SchoolManagementSystem.Data.Entities.User)entry.Entity;
                
                if (string.IsNullOrEmpty(user.FirstName))
                {
                    user.FirstName = "Member"; // 💡 Forces a default string fallback to satisfy NOT NULL constraints
                }
                
                if (string.IsNullOrEmpty(user.LastName))
                {
                    user.LastName = "User";
                }
            }

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // 💡 ALSO OVERRIDE SYNC SAVES TO MATCH PIPELINE EXECUTIONS
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is SchoolManagementSystem.Data.Entities.User && 
                           (e.State == Microsoft.EntityFrameworkCore.EntityState.Added || 
                            e.State == Microsoft.EntityFrameworkCore.EntityState.Modified));

            foreach (var entry in entries)
            {
                var user = (SchoolManagementSystem.Data.Entities.User)entry.Entity;
                if (string.IsNullOrEmpty(user.FirstName)) user.FirstName = "Member";
                if (string.IsNullOrEmpty(user.LastName)) user.LastName = "User";
            }

            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

}
