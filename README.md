# nicholas-script
## How to run
```shell
make
```


## return types
#### Addition (+) and Subtraction (-)
|        | int          | bool         | string       | list         | func |
|--------|--------------|--------------|--------------|--------------|------|
| int    | int          | int          | string       | element wise |      |
| bool   | int          | bool         | string       | element wise |      |
| string | string       | string       | string       | element wise |      |
| list   | element wise | element wise | element wise | element wise |      |
| func   |              |              |              |              |      |

#### Multiplication (*) and division(/) 
|        | int          | bool         | string       | list         | func       |
|--------|--------------|--------------|--------------|--------------|------------|
| int    | int          | int          | string       | element wise | loop       |
| bool   | int          | bool         |              | element wise | func/orNot |
| string | string       |              |              | element wise |            |
| list   | element wise | element wise | element wise | element wise |            |
| func   |              | func/orNot   |              |              |            |









