#!/bin/bash

name="$1"
groupSize=4

clang -c -o "${name}" "${name}.S" && \
objcopy -O binary -j .text "${name}" "${name}.bin" && \
od -An -tx1 -v "${name}.bin" | \
tr -d '\n' | \
awk -v groupSize="$groupSize" '{
    for (i = 1; i <= NF; i++) {
        printf "0x%s", toupper($i)
        printf ","
        if (i % groupSize == 0 && i < NF) {
            printf "\n"
        } else if (i < NF) {
            printf " "
        }
        if (i == NF) {
            printf "\n"
        }
    }
}'
