# SEUtils

#### A must have for your Space engineers scripts!

## [Documentation](../../wiki)

## Requirements
In order to be able to use these Utils properly, you are **required** to use the **[MDK for SE](https://github.com/malware-dev/MDK-SE)**

## Installation

* **Clone** this **repository** onto your computer.
* In your **ingame script project**, you need to **add** the **cloned project** to **your solution**. (Add/Existing project...)
* **Add** a **reference** to the **cloned project** (Add reference.../Shared Projects)
* Create a field of type **SEUtils** in your class **Program**
* Add this code at the beginning of your Scripts constructor
 ```c#
_yourSEUtilsField = new SEUtils();
```
* Add this code to the beginning your main method
```c#
if (!_yourSEUtilsField.RuntimeUpdate(argument, updateSource)) return;
```
### Now the setup is completed. You can continue by creating your first [Coroutine](../../wiki/Coroutines)
