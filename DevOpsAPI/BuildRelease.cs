using System;

namespace DevOpsAPI
{
    public class BuildRelease
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime? Queue { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? Finish { get; set; }
        public TimeSpan Wait { get; set; }
        public TimeSpan Build { get; set; }
        public string Status { get; set; }
        public bool Release { get; set; }
        public string URL { get; set; }
    }
}
