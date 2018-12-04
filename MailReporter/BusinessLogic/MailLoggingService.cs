using DomainModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repos;
using System.Text.RegularExpressions;

namespace BusinessLogic
{
    public class MailLoggingService
    {
        public JobExecutionRepo JobExecutionRepo { get; set; }

        public JobRepo JobRepo { get; set; }

        public MailLoggingService(string mongoDbConnectionString, string mongoDbDatabaseName)
        {
            JobExecutionRepo = new JobExecutionRepo(mongoDbConnectionString, mongoDbDatabaseName);
            JobRepo = new JobRepo(mongoDbConnectionString, mongoDbDatabaseName);
        }

        public async Task<JobExecution> ConvertMailToJobExecution(NotificationEmail mail)
        {
            //var allJobs = await JobRepo.GetAll();
            var allJobs = new List<Job>
            {
                new Job {Name = "Cinema ETL Live", SubjectRegex = "[Live] Cinema ETL"},
                new Job {Name = "OOH ETL Live", SubjectRegex = "OOH ETL Live"},
                new Job {Name = "Online ETL", SubjectRegex = "'Update Online'"},
                new Job {Name = "Radio ETL", SubjectRegex = "'Update Radio'"},
                new Job {Name = "Print ETL", SubjectRegex = "'Update Print'"},
                new Job {Name = "XV Routines", SubjectRegex = "'Xv Routines'"},
                new Job {Name = "Import MSW Online Data", SubjectRegex = "'Import MSW Online Data'"},
                new Job {Name = "Full Text Search Index", SubjectRegex = "[Xv.WebJobs.FullTextSearch][Live]"},
            };

            // Determine matching job
            Job job = null;
            // match by sender email address
            if (!string.IsNullOrWhiteSpace(mail.Sender))
            {
                job = allJobs.Where(j => j.EmailSender != null).FirstOrDefault(j => j.EmailSender.Equals(mail.Sender.Trim(), StringComparison.InvariantCultureIgnoreCase));
            }
            // Match by subject regex
            if (job == null)
            {
                job = allJobs.Where(j => j.SubjectRegex != null).FirstOrDefault(j => Regex.IsMatch(mail.Subject, j.SubjectRegex));
            }

            var jobExecution = new JobExecution
            {
                JobId = job.Id,
                JobName = job.Name,
                OriginalBody = mail.BodyHtml,
                OriginalSubject = mail.Subject,
                NotificationEmail = mail,
                Started = DateTime.UtcNow, // TODO
                Finished = DateTime.UtcNow,
                ErrorSummary = "" //TODO
            };

            // determine the status
            var errorWords = new List<string> {"failed", "error", "unsuccessful", "fehlgeschlagen", "fehler", "gescheitert", "nicht erfolgreich"};
            var successWords = new List<string> { "success", "succeeded", "completed", "erfolgreich", "abgeschlossed" };
            if (errorWords.Any(e => mail.Subject.ToLowerInvariant().Contains(e)))
            {
                jobExecution.Status = JobExecutionStatus.Error;
            }
            else if(successWords.Any(s => mail.Subject.ToLowerInvariant().Contains(s)))
            {
                jobExecution.Status = JobExecutionStatus.Success;
            }
            else
            {
                jobExecution.Status = JobExecutionStatus.Unknown;
            }

            return jobExecution;
        }

        

        public async Task SaveJobExecution(JobExecution jobExecution)
        {
            await JobExecutionRepo.Create(jobExecution);
        }

        public async Task SeedJobs()
        {

        }
    }
}
