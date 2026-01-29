# Excel 打开 CSV 文件中文乱码解决方案

## 方法一：使用 Excel 的数据导入功能（推荐）

1. 打开 Excel
2. 点击 **数据** → **从文本/CSV**
3. 选择 `GameText.csv` 文件
4. 在预览窗口中，将**文件原始格式**设置为 **UTF-8**
5. 点击 **加载** 或 **转换数据**（Power Query）
6. 如果使用 Power Query，可以进一步调整数据类型，然后点击 **关闭并加载**

## 方法二：使用记事本转换编码

1. 用记事本打开 `GameText.csv`
2. 点击 **文件** → **另存为**
3. 在编码下拉菜单中选择 **UTF-8 with BOM**
4. 保存文件
5. 用 Excel 打开（Excel 可以识别 UTF-8 with BOM）

## 方法三：使用其他编辑器

- **VS Code**：可以正确显示 UTF-8 编码的 CSV 文件
- **Notepad++**：可以设置编码并正确显示
- **Google Sheets**：在线打开，通常可以正确处理 UTF-8

## 注意事项

- Unity Localization 的 CSV 文件使用 UTF-8 编码
- Excel 默认使用系统编码（中文 Windows 通常是 GBK），所以直接打开会乱码
- 使用数据导入功能可以指定编码，是最可靠的方法

