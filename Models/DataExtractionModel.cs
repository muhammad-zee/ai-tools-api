using Microsoft.AspNetCore.Mvc;

namespace WeatherForecastAI.Models
{
    public class DataExtractionModel
    {
        public class ExtractedDocumentResult
        {
            public string DocumentType { get; set; } // Either "CV" or "Invoice"
            public DocumentConfidence Confidence { get; set; }
            public CvData CvDetails { get; set; }
            public InvoiceData InvoiceDetails { get; set; }
        }

        public class DocumentConfidence
        {
            public double Score { get; set; } // Between 0.0 and 1.0
            public string Reason { get; set; }
        }

        public class CvExtractionResult
        {
            public DocumentConfidence Confidence { get; set; }
            public CvData CvDetails { get; set; }
        }

        public class InvoiceExtractionResult
        {
            public DocumentConfidence Confidence { get; set; }
            public InvoiceData InvoiceDetails { get; set; }
        }

        // --- Structure for Resumes / CVs ---
        public class CvData
        {
            public string CandidateName { get; set; }
            public string ContactEmail { get; set; }
            public string ContactPhone { get; set; }
            public List<string> Skills { get; set; }
            public List<WorkExperience> Experience { get; set; }
            public List<EducationRecord> Education { get; set; }
        }

        public class WorkExperience
        {
            public string Company { get; set; }
            public string JobTitle { get; set; }
            public string Duration { get; set; } // e.g., "2022 - 2025"
            public string KeyResponsibilities { get; set; }
        }

        public class EducationRecord
        {
            public string Institution { get; set; }
            public string Degree { get; set; }
            public string GraduationYear { get; set; }
        }

        // --- Structure for Invoices ---
        public class InvoiceData
        {
            public string InvoiceNumber { get; set; }
            public string VendorName { get; set; }
            public string IssueDate { get; set; }
            public string DueDate { get; set; }
            public decimal? TotalAmount { get; set; }
            public string Currency { get; set; }
            public List<InvoiceLineItem> LineItems { get; set; }
        }

        public class InvoiceLineItem
        {
            public string ItemDescription { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal LineTotal { get; set; }
        }
    }
}
