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
            JobRepo = new JobRepo(); //(mongoDbConnectionString, mongoDbDatabaseName);
        }

        public JobExecution ConvertMailToJobExecution(NotificationEmail mail)
        {
            var allJobs = JobRepo.GetAll();

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
                var jobs = allJobs.Where(j => !string.IsNullOrWhiteSpace(j.SubjectRegex) && Regex.IsMatch(mail.Subject, j.SubjectRegex));
                if(jobs.Count() > 1)
                {
                    job = new Job { Name = "Ambiguous Subject RegEx: " + string.Join(" ,", jobs.Select(j => j.Name))};
                }
                job = jobs.FirstOrDefault();
            }

            // Match by subject contains
            if (job == null)
            {
                var jobs = allJobs.Where(j => !string.IsNullOrWhiteSpace(j.SubjectContains) && mail.Subject.Contains(j.SubjectContains));
                if (jobs.Count() > 1)
                {
                    job = new Job { Name = "Ambiguous Subject Contains: " + string.Join(" ,", jobs.Select(j => j.Name)) };
                }
                job = jobs.FirstOrDefault();
            }

            if (job == null)
            {
                job = new Job { Name = "Unknown" };
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
            var successWords = new List<string> { "success", "succeeded", "completed", "erfolgreich", "abgeschlossed", "erfolg" };

            if(job.ErrorSubjectRegex != null && job.SuccessSubjectRegex != null)
            {
                if (job.ErrorSubjectRegex != null && Regex.IsMatch(mail.Subject, job.ErrorSubjectRegex))
                {
                    jobExecution.Status = JobExecutionStatus.Error;
                }
                else if(job.SuccessSubjectRegex != null && Regex.IsMatch(mail.Subject, job.SuccessSubjectRegex))
                {
                    jobExecution.Status = JobExecutionStatus.Success;
                }
                else
                {
                    jobExecution.Status = JobExecutionStatus.Unknown;
                }
            }
            else
            {
                if (errorWords.Any(e => mail.Subject.ToLowerInvariant().Contains(e)))
                {
                    jobExecution.Status = JobExecutionStatus.Error;
                }
                else if (successWords.Any(s => mail.Subject.ToLowerInvariant().Contains(s)))
                {
                    jobExecution.Status = JobExecutionStatus.Success;
                }
                else
                {
                    jobExecution.Status = JobExecutionStatus.Unknown;
                }
            }
            

            return jobExecution;
        }

        

        public async Task SaveJobExecution(JobExecution jobExecution)
        {
            await JobExecutionRepo.Create(jobExecution);
        }
    }
}
