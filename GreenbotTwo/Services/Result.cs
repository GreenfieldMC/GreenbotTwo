using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace GreenbotTwo.Services;

public class Result<T>
{
    
    /// <summary>
    /// The data returned from the operation. Default if the operation failed.
    /// </summary>
    public required T? Data { get; set; }
    
    /// <summary>
    /// Indicates whether the operation was successful. Under normal usage, a failed operation is one that throws an Exception and fails to return a result.
    /// </summary>
    public required bool IsSuccessful { get; set; }
    
    /// <summary>
    /// The error message if the operation failed. Null if the operation was successful.
    /// </summary>
    public required string? ErrorMessage { get; set; }

    /// <summary>
    /// The HTTP status code representing the result of the operation.
    /// </summary>
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    
    public static Result<T> Success(T data, HttpStatusCode statusCode = HttpStatusCode.OK) => new()
    {
        Data = data,
        IsSuccessful = true,
        ErrorMessage = null,
        StatusCode = statusCode
    };
    
    public static Result<T> Failure(string errorMessage, HttpStatusCode statusCode = HttpStatusCode.BadRequest) => new()
    {
        Data = default,
        IsSuccessful = false,
        ErrorMessage = errorMessage,
        StatusCode = statusCode
    };
    
    /// <summary>
    /// Returns the StatusCode as an integer.
    /// </summary>
    /// <returns></returns>
    public int GetStatusCodeInt()
    {
        return (int)StatusCode;
    }
    
    public void ThrowIfFailed()
    {
        if (!IsSuccessful)
        {
            throw new Exception(ErrorMessage);
        }
    }
    
    /// <summary>
    /// Returns true if the Data property is null.
    /// </summary>
    /// <returns></returns>
    public bool IsDataNull()
    {
        return Data is null;
    }

    /// <summary>
    /// If the operation did not succeed, throw an exception with the provided message or the error message from the Result.
    /// <br/>
    /// Note: If the operation did not throw an Exception, the Data property may still be null even if IsSuccessful is true.
    /// </summary>
    /// <param name="message">Optional custom message for the exception.</param>
    /// <returns></returns>
    /// <exception cref="Exception">Thrown if the operation was not successful.</exception>
    public T? GetOrThrow(string? message = null)
    {
        return !IsSuccessful ? throw new Exception(message ?? ErrorMessage) : Data;
    }

    /// <summary>
    /// If the operation did not succeed, throw an exception with the provided unsuccessful message or the error message from the Result.
    /// If the operation was successful but the Data is null, throw an exception with the provided null data message or a default message.
    /// <br/>
    /// Note: This should never return null.
    /// </summary>
    /// <param name="unsuccessfulMessage">The message to use if the operation was not successful.</param>
    /// <param name="nullDataMessage">The message to use if the operation was successful but the Data is null.</param>
    /// <returns></returns>
    /// <exception cref="Exception">Thrown if the operation was not successful or if the Data is null.</exception>
    [return: NotNull]
    public T GetNonNullOrThrow(string? unsuccessfulMessage = null, string? nullDataMessage = null)
    {
        return !IsSuccessful
            ? throw new Exception(unsuccessfulMessage ?? ErrorMessage)
            : Data ?? throw new Exception(nullDataMessage ?? "Operation returned null data.");
    }
    
    /// <summary>
    /// If the operation did not succeed, throw an exception created by the provided factory function.
    /// <br/>
    /// Note: If the operation did not throw a Exception, the Data property may still be null even if IsSuccessful is true.
    /// </summary>
    /// <param name="exceptionFactory">The factory function to create the exception to throw.</param>
    /// <returns></returns>
    /// <exception cref="Exception">>Thrown if the operation was not successful.</exception>
    public T? GetOrThrow(Func<Exception> exceptionFactory) 
    {
        return !IsSuccessful ? throw exceptionFactory() : Data;
    }
    
    /// <summary>
    /// If the operation did not succeed, return the provided default value.
    /// <br/>
    /// Note: If the operation did not throw an Exception, the Data property may still be null even if IsSuccessful is true.
    /// </summary>
    /// <param name="defaultValue">The default value to return if the operation was not successful.</param>
    /// <returns></returns>
    public T? GetOrDefault(T? defaultValue = default)
    {
        return !IsSuccessful ? defaultValue : Data;
    }
    
    /// <summary>
    /// Attempts to get the data if the operation was successful.
    /// </summary>
    /// <param name="data">The output data if successful; default value otherwise. Null values possible.</param>
    /// <returns></returns>
    public bool TryGetData(out T? data)
    {
        if (IsSuccessful)
        {
            data = Data;
            return true;
        }

        data = default;
        return false;
    }
    
    /// <summary>
    /// Attempts to get the data if the operation was successful and the data is not null.
    /// </summary>
    /// <param name="data">The output data if successful and non-null; default value otherwise.</param>
    /// <returns></returns>
    public bool TryGetDataNonNull([MaybeNullWhen(false)] out T data)
    {
        if (IsSuccessful && !IsDataNull())
        {
            data = Data!;
            return true;
        }

        data = default!;
        return false;
    }
    
}