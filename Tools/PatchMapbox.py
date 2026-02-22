
import os

file_path = "../mapbox-unity-sdk-v3/Runtime/Mapbox/BaseModule/Utilities/MapboxConfiguration.cs"
with open(file_path, 'r') as f:
    lines = f.readlines()

# Find the last #endif
last_endif_index = -1
for i in range(len(lines) - 1, -1, -1):
    if "#endif" in lines[i]:
        last_endif_index = i
        break

if last_endif_index != -1:
    # Insert #else block before #endif
    new_lines = [
        "\t\t#else\n",
        "\t\t\tpublic void Initialize() {}\n",
        "\t\t\tpublic string GetMapsSkuToken() { return \"000000\"; }\n"
    ]
    lines[last_endif_index:last_endif_index] = new_lines
    
    with open(file_path, 'w') as f:
        f.writelines(lines)
    print("Successfully patched MapboxConfiguration.cs")
else:
    print("Could not find #endif")
