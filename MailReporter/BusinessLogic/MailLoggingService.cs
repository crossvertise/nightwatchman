using DomainModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Repos;

namespace BusinessLogic
{
    public class MailLoggingService
    {
        public JobExecutionRepo JobExecutionRepo { get; set; }

        public JobRepo JobRepo { get; set; }


        public async Task<JobExecution> ConvertMailToJobExecution(NotificationEmail mail)
        {
            var allJobs = await JobRepo.GetAll();

            // Determine matching job
            Job job = null;
            if (!string.IsNullOrWhiteSpace(mail.Sender))
            {
                job = allJobs.FirstOrDefault(j => j.EmailSender.Equals(mail.Sender.Trim(), StringComparison.InvariantCultureIgnoreCase));
            }

            var jobRef = job != null ? new Tuple<string, string>(job.Id, job.Name) : new Tuple<string, string>("", "");

            var jobExecution = new JobExecution
            {
                Job = jobRef,
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
    }
}
