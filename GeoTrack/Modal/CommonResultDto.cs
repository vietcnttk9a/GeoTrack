using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoTrack.Modal
{
    public static class CommonResultDtoUtil
    {
        public static CommonResultDto<TClone> CloneError<TClone>(this CommonResultErrorDto input)
        {
            return new CommonResultDto<TClone>()
            {
                IsSuccessful = input.IsSuccessful,
                ErrorDetail = input.ErrorDetail,
                Notification = input.Notification,
                Errors = input.Errors,
                // ErrorTranslate = input.ErrorTranslate
            };
        }
    }

    public class CommonResultErrorDto
    {
        public bool IsSuccessful { get; set; } = false;
        public CommonResultExtendDto ErrorDetail { get; set; }
        public CommonResultExtendDto Notification { get; set; }
        public List<ValidateInputDto> Errors { get; set; } = new List<ValidateInputDto>();
        public string Message { get; set; }

    }
    public class CommonResultDto<T> : CommonResultErrorDto
    {
        public T Data { get; set; }
        public object ReturnValueAdditional { get; set; }
        public static CommonResultDto<T> Failed(string errorMessage, object notificationData = null, string errorCode = "", params string[] paramMessage)
        {
            return new CommonResultDto<T>()
            {
                IsSuccessful = false,
                ErrorDetail = new CommonResultExtendDto(errorMessage)
                {
                    Code = errorCode,
                    ParamMessage = paramMessage
                },
                Notification = new CommonResultExtendDto(errorMessage, notificationData, paramMessage),
            };
        }
        public static CommonResultDto<T> Failed(Exception ex)
        {
            return new CommonResultDto<T>()
            {
                IsSuccessful = false,
                ErrorDetail = new CommonResultExtendDto(ex.Message)
                {
                    Code = "ThrowEx_500",
                }
            };
        }
        public static CommonResultDto<T> Failed(Exception ex, params string[] paramMessage)
        {
            return new CommonResultDto<T>()
            {
                IsSuccessful = false,
                ErrorDetail = new CommonResultExtendDto(ex.Message)
                {
                    Code = "ThrowEx_500",
                    ParamMessage = paramMessage
                }
            };
        }
        public static CommonResultDto<T> Failed(List<ValidateInputDto> errorValid)
        {
            return new CommonResultDto<T>()
            {
                IsSuccessful = false,
                Errors = errorValid
            };
        }
        public static CommonResultDto<T> Ok(T dataSuccess, string notficationMessage = "", object? notificationData = null, object? returnValueAdditional = null, params string[] paramMessage)
        {
            var ret = new CommonResultDto<T>()
            {
                IsSuccessful = true,
                Data = dataSuccess,
                ReturnValueAdditional = returnValueAdditional
            };
            if (notficationMessage != "")
            {
                ret.Notification = new CommonResultExtendDto(notficationMessage,
                    notificationData == null ? dataSuccess : notificationData, paramMessage);
            }
            return ret;
        }
    }

    public class ValidateInputDto
    {
        public string PropertyName { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }
    }

    public class CommonResultExtendDto
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
        public string[] ParamMessage { get; set; }
        public object Data { get; set; }
        public CommonResultExtendDto() { }

        public CommonResultExtendDto(string message, object data = null)
        {
            Message = message;
            Data = data;
        }
        public CommonResultExtendDto(string message, object data = null, params string[] paramMessage)
        {
            Message = message;
            Data = data;
            ParamMessage = paramMessage;
        }
    }
}
