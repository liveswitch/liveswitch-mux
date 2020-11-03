/**
 * Apply a circular layout.
 */
function layout(inputs, output) {
    
    var center = {
        x: output.size.width / 2,
        y: output.size.height / 2
    };
    
    var radius = Math.min(output.size.width, output.size.height) / 4;
    
    // the top of the circle
    var angle = 1.5 * Math.PI;

    var frames = [];
    for (var i = 0; i < inputs.length; i++) {
        frames.push({
            origin: {
                x: (radius * Math.cos(angle) + center.x) - (radius / 2),
                y: (radius * Math.sin(angle) + center.y) - (radius / 2),
            },
            size: {
                width: radius,
                height: radius
            }
        });
        angle += (2 * Math.PI / inputs.length);
    }
    return frames;
}