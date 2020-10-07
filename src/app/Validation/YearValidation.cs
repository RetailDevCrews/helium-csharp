﻿using System;
using System.ComponentModel.DataAnnotations;

namespace CSE.Helium.Validation
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class YearValidation : ValidationAttribute
    {
        private const int StartYear = 1874;
        private static readonly int EndYear = DateTime.UtcNow.AddYears(5).Year;

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            _ = validationContext ?? throw new ArgumentNullException(nameof(validationContext));

            bool isValid = (int)value >= StartYear && (int)value <= EndYear || (int)value == 0;

            string errorMessage = $"The parameter '{validationContext.MemberName}' should be between {StartYear} and {EndYear}.";

            return !isValid ? new ValidationResult(errorMessage) : ValidationResult.Success;
        }
    }
}
