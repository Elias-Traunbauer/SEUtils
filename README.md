# SEUtils

This is a class that provides a set of useful methods for your Space Engineers scripts!

## [Features](../../wiki)

## Requirements
In order to be able to use these Utils properly, you are **required** to use the **[MDK for SE](https://github.com/malware-dev/MDK-SE)**

## Installation

* **Clone** this **repository** onto your computer.
* In your **ingame script project**, you need to **add** the **cloned project** to **your solution**. (Add/Existing project...)
* **Add** a **reference** to the **cloned project** (Add reference.../Shared Projects)

## Usage

**Before** you can **use SEUtils** you **need** to do **two** more **things**:
* Add this code at the beginning of your Scripts constructor `SEUtils.Setup(this);`
* Add this code at the beginning of your main method `SEUtils.RuntimeUpdate(argument, updateSource);`

Now you are ready to use SEUtils!
