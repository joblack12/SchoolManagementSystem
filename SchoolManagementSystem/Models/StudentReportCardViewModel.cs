namespace SchoolManagementSystem.ViewModels
{
    public class StudentReportCardViewModel
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string AdmissionNumber { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string Term { get; set; } = "Term 1";
        public int AcademicYear { get; set; } = DateTime.Now.Year;

        public List<SubjectGradeItem> Subjects { get; set; } = new();

        // Calculated Aggregates (0 - 20 Scale)
        public double TotalScore => Math.Round(Subjects.Sum(s => s.Score), 2);
        public double MeanScore => Subjects.Any() ? Math.Round(Subjects.Average(s => s.Score), 2) : 0;
        public int TotalCredits => Subjects.Sum(s => s.Credits);
        public string OverallStatus => MeanScore >= 9.5 ? "Passed" : "Failed";
        public string OverallGrade => CalculateLetterGrade(MeanScore);
        public string TeacherRemarks => GetAutomatedRemarks(MeanScore);

        public static string CalculateLetterGrade(double score)
        {
            return score switch
            {
                >= 17.5 => "A (Distinction)",
                >= 15.0 => "B (Very Good)",
                >= 12.5 => "C (Good)",
                >= 9.5 => "D (Pass)",
                _ => "F (Fail)"
            };
        }

        private static string GetAutomatedRemarks(double mean)
        {
            return mean switch
            {
                >= 17.5 => "Outstanding performance. Keep up the high standard.",
                >= 14.0 => "Good work. Consistent effort across subjects.",
                >= 9.5 => "Satisfactory pass. Aim higher next term.",
                _ => "Below pass threshold (9.5/20). Extra academic support recommended."
            };
        }
    }

    public class SubjectGradeItem
    {
        public string SubjectCode { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public int Credits { get; set; }
        public double Score { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Grade => StudentReportCardViewModel.CalculateLetterGrade(Score);
    }
}
