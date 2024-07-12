function generateMatrices() {
    var rows = document.querySelector('input[name=rows]').value;
    var cols = document.querySelector('input[name=cols]').value;

    var matrixA = document.querySelector('textarea[name=matrixA]');
    var matrixB = document.querySelector('textarea[name=matrixB]');

    matrixA.value = generateMatrix(rows, cols);
    matrixB.value = generateMatrix(rows, cols);
}

function generateMatrix(rows, cols) {
    var matrix = '';
    for (var i = 0; i < rows; i++) {
        for (var j = 0; j < cols; j++) {
            matrix += Math.floor(Math.random() * 10) + ' ';
        }
        matrix += '\n';
    }
    return matrix;
}
