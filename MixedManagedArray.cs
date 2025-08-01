// Copyright 2025 Briganti. See LICENSE file for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

/// <summary>
/// Array that can be used as both a Native and a Managed array without needing a copy
/// </summary>
/// <typeparam name="T">Any unmanaged type</typeparam>
public class MixedManagedArray<T> : IDisposable where T : unmanaged
{
	public MixedManagedArray(int length) : this(new T[length])
	{
	}

	public MixedManagedArray(T[] array)
	{
		this.length = array.Length;
		this.array = array;
		this.handle = default;
		unsafe { this.ptr = default; }
	}

	public MixedManagedArray(IEnumerable<T> elements)
	{
		this.array = elements.ToArray();
		this.length = array.Length;
		this.handle = default;
		unsafe { this.ptr = default; }
	}

	// Make sure the handle gets freed eventually
	~MixedManagedArray() { Dispose(); }

	public void Dispose()
	{
		if (this.array == null) // already disposed
			return;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
		{
			AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
			AtomicSafetyHandle.Release(m_Safety);
		}
#endif
		if (handle.IsAllocated) handle.Free();
		this.length = 0;
		this.array = null;
		this.handle = default;
		unsafe { this.ptr = default; }
	}

	public int Length => length;
	int length;

	T[] array;
	GCHandle handle;
	unsafe T* ptr;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
	AtomicSafetyHandle m_Safety;
#endif

	// Note: NativeArray can only be used in Jobs between RetrieveNativeArray and ReturnNativeArray. MixedArray can only be used outside of RetrieveNativeArray and ReturnNativeArray.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public NativeArray<T> RetrieveNativeArray()
	{
		if (handle.IsAllocated) throw new InvalidOperationException("Retrieving NativeArray, but it wasn't Returned before");
		this.handle = GCHandle.Alloc(array, GCHandleType.Pinned);
		unsafe
		{
			this.ptr = (T*)(this.handle.AddrOfPinnedObject()).ToPointer();
			var result = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(this.ptr, this.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (AtomicSafetyHandle.IsDefaultValue(m_Safety))
				m_Safety = AtomicSafetyHandle.Create();
			NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref result, m_Safety);
#endif
			return result;
		}
	}

	// Note: NativeArray can only be used in Jobs between RetrieveNativeArray and ReturnNativeArray. MixedArray can only be used outside of RetrieveNativeArray and ReturnNativeArray.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public NativeArray<T>.ReadOnly RetrieveReadOnlyNativeArray()
	{
		return RetrieveNativeArray().AsReadOnly();
	}

	// Note: can only return a native array after all the jobs that use it have been finished! NativeArray can only be used in Jobs between RetrieveNativeArray and ReturnNativeArray. 
	//		 MixedArray can only be used outside of RetrieveNativeArray and ReturnNativeArray.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ReturnNativeArray(ref NativeArray<T> returnedArray)
	{
		if (!handle.IsAllocated) throw new InvalidOperationException("Returning NativeArray, but it wasn't Retrieved before");
		if (!returnedArray.IsCreated) throw new ArgumentException("Returned NativeArray has been disposed and cannot be returned", nameof(returnedArray));
		unsafe
		{
			if (this.ptr != (T*)returnedArray.GetUnsafePtr())
				throw new ArgumentException("Returned NativeArray is not the same NativeArray as was retrieved", nameof(returnedArray));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (AtomicSafetyHandle.IsDefaultValue(m_Safety))
				throw new ArgumentException("Returned NativeArray has not been initialized properly");
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		}
		returnedArray = default;
		handle.Free();
		unsafe { this.ptr = null; }
	}

	// Note: can only return a native array after all the jobs that use it have been finished! NativeArray can only be used in Jobs between RetrieveNativeArray and ReturnNativeArray. 
	//		 MixedArray can only be used outside of RetrieveNativeArray and ReturnNativeArray.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ReturnNativeArray(ref NativeArray<T>.ReadOnly returnedArray)
	{
		if (!handle.IsAllocated) throw new InvalidOperationException("Returning NativeArray, but it wasn't Retrieved before");
		if (!returnedArray.IsCreated) throw new ArgumentException("Returned NativeArray has been disposed and cannot be returned", nameof(returnedArray));
		unsafe
		{
			if (this.ptr != (T*)returnedArray.GetUnsafeReadOnlyPtr())
				throw new ArgumentException("Returned NativeArray is not the same NativeArray as was retrieved", nameof(returnedArray));
#if ENABLE_UNITY_COLLECTIONS_CHECKS
			if (AtomicSafetyHandle.IsDefaultValue(m_Safety))
				throw new ArgumentException("Returned NativeArray has not been initialized properly");
			AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
		}
		returnedArray = default;
		handle.Free();
		unsafe { this.ptr = null; }
	}

	public ref T this[int index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			unsafe
			{
				if (ptr != null) ThrowAccessViolationException();
				return ref array[index];
			}
		}
	}

	// Seperate methods for throws so that the methods above are smaller and have fewer instruction cache misses
	[System.Diagnostics.CodeAnalysis.DoesNotReturn] void ThrowAccessViolationException() { throw new AccessViolationException("Referencing an element in a MixedManagedArray that is currently being used natively."); }
}
