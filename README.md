# TooD
A 2D lighting Unity asset using a technique similar to DDGI but in 2d. Example Image
![example image](https://i.imgur.com/1xnoY1y.png)
Video Example: https://www.youtube.com/watch?v=0YfO5mFXPos

This project is heavily inspired by this paper: http://jcgt.org/published/0008/02/01/paper-lowres.pdf

# Note
**This asset replaces the rendering pipeline with a custom one specifically for rendering this. Shaders must be edited to work with this asset.**

# Setting Up Custom Shaders
To setup a custom shader to work with this asset

Make sure your shader is compatible with the Universal Render Pipeline
Add Tags `{ "LightMode" = "Universal2D" }` to your main pass of your shader
Create a new pass with `Tags { "LightMode" = "TooDLighting" }` the return value of this pass should be a float4 with the first 3 components being the light color and the last component being exactly `1` if it's a wall, and `0` if it's not a wall.
I recommend looking at `/Assets/TooD/Shaders/TooD Sprite.shader` for reference when setting this up.
