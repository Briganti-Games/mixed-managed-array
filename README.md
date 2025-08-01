# mixed-managed-array
An array implementation for Unity that works both in burst and in managed code without having to copy over data.

Native arrays are slower when used in Managed code, but faster/required in Native code in Unity.
This data structure allows you to have the best of both worlds; 
- Use a Managed array while doing work in managed code
- Use a Native array while doing work in native code
- Not needing to copy any data

Notes:
- Native Arrays need to be retrieved and then returned when no longer used. 
- The Managed array cannot be used at the same time as the Native Array.
- The MixedManagedArray needs to be disposed at the end of its life

## Usage
```C#
int arraySize = 10;

// Allocate the array
MixedManagedArray<int> myArray = new MixedManagedArray<int>(arraySize);

// Write to the Managed array
for (int i = 0; i < arraySize; i++)
	myArray[arraySize] = i;


NativeArray<int> myNativeArray = myArray.RetrieveNativeArray();
// Note: can also use RetrieveReadOnlyNativeArray() to retrieve a NativeArray<T>.ReadOnly instead
// Note: CANNOT use myArray until we return myNativeArray

// .. do some work on Native array
for (int i = 0; i < myNativeArray.Length; i++)
	myNativeArray[arraySize] = arraySize - i;

// Return the native array to make use myArray knows that we can use the Managed array again
// Note: do not try to dispose myNativeArray!
myArray.ReturnNativeArray(ref myNativeArray);


// Note: From this point on
// - myNativeArray CANNOT be used anymore
// - it's safe to use the Managed array (myArray) again

// Read from Managed array
for (int i = 0; i < arraySize; i++)
  Debug.Log(myArray[arraySize]);

```

# License
mixed-managed-array is developed by Sander van Rossen (Briganti) and uses the MIT license.
