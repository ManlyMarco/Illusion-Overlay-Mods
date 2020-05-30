## How to edit body masks
Body masks are used to hide parts of the character's body to prevent clipping with clothes. You can change them in maker at the bottom of some clothes tabs. You need to have a recent version of overlay mod for the feature to be available.

Images used as mask should use two colors - red and green. You can see an example by exporting an existing mask in maker.
- Red - controls if the body / inner clothes should be visible when top clothes are fully on.
- Green - controls if the body / inner clothes should be visible when top clothes are partially off.
- Yellow - it's actually red and green mixed together, it will be visible in both states.
- Black - not visible in both states, but still visible when clothes are fully taken off.

Values near 0 will hide the body/clothes, while values near max/FF will show them. Values in-between are not recommended. Red color (clothes fully on) is rarely seen because it usually overlaps with green (clothes partially off) and creates the yellow color. Blue color is ignored in most cases but should not be used as it can potentially cause issues with some shaders.
