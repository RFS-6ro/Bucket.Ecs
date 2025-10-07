using System;

namespace Bucket.Ecs.v3
{
    public static class BAssert
    {
        [System.Diagnostics.DebuggerStepThrough]
        public static void True(bool condition, string message = null)
        {
            if (!condition) BExceptionThrower.Assert(message);
        }



        [System.Diagnostics.DebuggerStepThrough]
        public static void False(bool condition, string message = null)
        {
            if (condition != false) BExceptionThrower.Assert(message);
        }



        [System.Diagnostics.DebuggerStepThrough]
        public static void CanAccess(bool condition, string message = null)
        {
            if (!condition) BExceptionThrower.InvalidMemoryAccess(message);
        }



        [System.Diagnostics.DebuggerStepThrough]
        public static void IsEmpty(int size, string message = null)
        {
            if (size != 0) BExceptionThrower.Assert(message);
        }



        [System.Diagnostics.DebuggerStepThrough]
        public static void IsNotEmpty(int size, string message = null)
        {
            if (size == 0) BExceptionThrower.Assert(message);
        }



        [System.Diagnostics.DebuggerStepThrough]
        public static void IndexInRange(int index, int max, string message = null)
        {
            if (index < 0 || index >= max) BExceptionThrower.OutOfRange(message);
        }



        [System.Diagnostics.DebuggerStepThrough]
        public static void InRange(int index, int min, int max, string message = null)
        {
            if (index < min || index >= max) BExceptionThrower.OutOfRange(message);
        }
    }

    public static class BExceptionThrower
    {
        [System.Diagnostics.DebuggerStepThrough]
        public static void Assert(string message = null)
        {
            if (string.IsNullOrEmpty(message)) throw new BAssertException();
            else throw new BAssertException(message);
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static void OutOfRange(string message = null)
        {
            if (string.IsNullOrEmpty(message)) throw new BOutOfRangeException();
            else throw new BOutOfRangeException(message);
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static void CodeGenerationFailed(string message = null)
        {
            if (string.IsNullOrEmpty(message)) throw new BCodeGenerationFailedException();
            else throw new BCodeGenerationFailedException(message);
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static void ObjectNotDisposed(string message = null)
        {
            if (string.IsNullOrEmpty(message)) throw new BObjectNotDisposedException();
            else throw new BObjectNotDisposedException(message);
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static void ObjectIsDisposed(string message = null)
        {
            if (string.IsNullOrEmpty(message)) throw new BObjectIsDisposedException();
            else throw new BObjectIsDisposedException(message);
        }
        
        [System.Diagnostics.DebuggerStepThrough]
        public static void EntityHasNoComponent(string message = null)
        {
            if (string.IsNullOrEmpty(message)) throw new BInvalidOperationException();
            else throw new BInvalidOperationException(message);
        }
        
        [System.Diagnostics.DebuggerStepThrough]
        public static void ComponentAlreadyAttached(string message = null)
        {
            if (string.IsNullOrEmpty(message)) throw new BInvalidOperationException();
            else throw new BInvalidOperationException(message);
        }
        
        [System.Diagnostics.DebuggerStepThrough]
        public static void FilterIsEmpty(string message = null)
        {
            if (string.IsNullOrEmpty(message)) throw new BFilterIsEmptyException();
            else throw new BFilterIsEmptyException(message);
        }
        
        [System.Diagnostics.DebuggerStepThrough]
        public static void EntityIsNotSingle(string message = null)
        {
            if (string.IsNullOrEmpty(message)) throw new BEntityIsNotSingleException();
            else throw new BEntityIsNotSingleException(message);
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static void SchedulerIsNotAssigned(string message = null)
        {
            if (string.IsNullOrEmpty(message)) throw new BSchedulerIsNotAssignedException();
            else throw new BSchedulerIsNotAssignedException(message);
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static void InvalidMemoryAccess(string message = null)
        {
            if (string.IsNullOrEmpty(message)) throw new BInvalidMemoryAccessException();
            else throw new BInvalidMemoryAccessException(message);
        }
    }

    public class BException :  Exception
    {
        public BException()
        { }

        public BException(string message) : base(message)
        { }
    }

    public class BAssertException :  BException
    {
        public BAssertException()
        { }

        public BAssertException(string message) : base(message)
        { }
    }

    public class BOutOfRangeException :  BException
    {
        public BOutOfRangeException()
        { }

        public BOutOfRangeException(string message) : base(message)
        { }
    }

    public class BCodeGenerationFailedException :  BException
    {
        public BCodeGenerationFailedException()
        { }

        public BCodeGenerationFailedException(string message) : base(message)
        { }
    }

    public class BObjectNotDisposedException :  BException
    {
        public BObjectNotDisposedException()
        { }

        public BObjectNotDisposedException(string message) : base(message)
        { }
    }

    public class BObjectIsDisposedException :  BException
    {
        public BObjectIsDisposedException()
        { }

        public BObjectIsDisposedException(string message) : base(message)
        { }
    }

    public class BInvalidOperationException :  BException
    {
        public BInvalidOperationException()
        { }

        public BInvalidOperationException(string message) : base(message)
        { }
    }

    public class BFilterIsEmptyException :  BException
    {
        public BFilterIsEmptyException()
        { }

        public BFilterIsEmptyException(string message) : base(message)
        { }
    }

    public class BEntityIsNotSingleException :  BException
    {
        public BEntityIsNotSingleException()
        { }

        public BEntityIsNotSingleException(string message) : base(message)
        { }
    }

    public class BSchedulerIsNotAssignedException : BException
    {
        public BSchedulerIsNotAssignedException()
        { }

        public BSchedulerIsNotAssignedException(string message) : base(message)
        { }
    }

    public class BInvalidMemoryAccessException : BException
    {
        public BInvalidMemoryAccessException()
        { }

        public BInvalidMemoryAccessException(string message) : base(message)
        { }
    }
}
