﻿using Bucket.Core;
using Bucket.ErrorCode;
using Bucket.Exceptions;
using Bucket.Tracer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
namespace Bucket.AspNetCore.Middleware.Error
{
    public class ErrorLogMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IErrorCodeStore _errorCodeStore;
        private readonly IJsonHelper _jsonHelper;
        public ErrorLogMiddleware(RequestDelegate next, 
            ILoggerFactory loggerFactory, 
            IErrorCodeStore errorCodeStore,
            IJsonHelper jsonHelper)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<ErrorLogMiddleware>();
            _errorCodeStore = errorCodeStore;
            _jsonHelper = jsonHelper;
        }

        public async Task Invoke(HttpContext context)
        {
            ErrorResult errorInfo = null;
            try
            {
                await _next(context);
            }
            catch (BucketException ex)
            {
                var newMsg = _errorCodeStore.StringGet(ex.ErrorCode);
                if (string.IsNullOrWhiteSpace(newMsg))
                    newMsg = ex.ErrorMessage;
                errorInfo = new ErrorResult(ex.ErrorCode, newMsg);
            }
            catch (Exception ex)
            {
                errorInfo = new ErrorResult("-1", "系统开小差了,请稍后再试");
                var span = context.GetSpan();
                if (span != null)
                {
                    span.Exception(ex);
                    context.SetSpan(span);
                }
                _logger.LogError(ex, $"全局异常捕获，状态码：{ context?.Response?.StatusCode}，Url：{context?.Request?.GetDisplayUrl()}");
            }
            finally
            {
                if (errorInfo != null)
                {
                    var Message = JsonConvert.SerializeObject(errorInfo);
                    await HandleExceptionAsync(context, Message);
                }
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, string message)
        {
            context.Response.ContentType = "application/json;charset=utf-8";
            return context.Response.WriteAsync(message);
        }
    }
}
