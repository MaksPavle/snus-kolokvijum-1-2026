# Industrial Processing System

This project was developed as part of the SNUS course and represents a simulation of an industrial job processing system.

The system is based on a producer-consumer model and supports asynchronous execution, priority scheduling and safe concurrent access.

## Key Characteristics

- Thread-safe design using `ConcurrentDictionary` and synchronization mechanisms
- Priority-based processing (lower value = higher priority)
- Asynchronous execution using `Task` and worker loops
- Retry logic (up to 3 attempts per job)
- Timeout control (2 seconds per execution)
- Event-driven logging system
- XML-based configuration
- Periodic reporting using LINQ

## Job Types

- **Prime**
  - Calculates number of prime numbers up to a given value
  - Uses parallel processing (`Parallel.For`)
  - Thread count limited to range [1–8]

- **IO**
  - Simulates external IO using delay
  - Returns a random value between 0 and 100

## Configuration

System parameters are loaded from XML:
- Number of worker threads
- Maximum queue size
- Initial set of jobs

## Logging

System uses event-based logging:
- `JobCompleted`
- `JobFailed`
- `ABORT` (after all retry attempts fail)

Example format:
[DateTime] [STATUS] JobId, Result

For failed jobs, the log includes additional information about the error and retry attempt.

## Report

A report is generated periodically and includes:
- Number of successfully executed jobs per type
- Average execution time per type
- Number of failed jobs (only final failures after retries)

## Additional Notes

- Failed attempts are logged separately, but only jobs that fail after all retries are counted as failed in reports.
- The system uses `TaskCompletionSource` for asynchronous result handling without blocking.
- `Thread.Sleep` is used only for IO simulation, not for synchronization or waiting.

## Author

Pavle Maksimović SV 58/2023


