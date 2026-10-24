import os
import sys
import re

def extract_number_from_line(line):
    """
    从行中提取'items'前的数字
    示例输入: "  0 Array Array (1 items)"
    返回: 1
    """
    match = re.search(r'\((\d+)\s+items\)', line)
    if match:
        return int(match.group(1))
    return 0

def process_file(file_path):
    """
    处理单个文件，查找包含"m_NonModifiableTextures"的行及其下一行
    返回: 如果找到且数字大于0，返回数字；否则返回0
    """
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.readlines()
    except Exception as e:
        print(f"无法读取文件 {file_path}: {e}")
        return 0

    # 从文件末尾开始检查（因为目标行在尾部）
    for i in range(len(content) - 2, -1, -1):
        if "m_NonModifiableTextures" in content[i]:
            # 检查下一行是否包含"items"
            if i + 1 < len(content) and "items" in content[i + 1]:
                items_count = extract_number_from_line(content[i + 1])
                if items_count > 0:
                    return items_count
    return 0

def get_filename_without_extension(file_path):
    """
    从文件路径中提取不带扩展名的文件名
    """
    # 获取文件名（带扩展名）
    filename_with_ext = os.path.basename(file_path)
    # 去除扩展名
    filename_without_ext = os.path.splitext(filename_with_ext)[0]
    return filename_without_ext

def main():
    if len(sys.argv) < 2:
        input("请将文件夹拖拽到脚本上，然后按回车键继续...")
        return
    
    folder_path = sys.argv[1]
    
    if not os.path.isdir(folder_path):
        print("错误：提供的路径不是一个文件夹")
        return
    
    print(f"正在处理文件夹: {folder_path}")
    
    # 存储符合条件的文件名和对应的数字
    matching_files = []
    
    # 遍历文件夹中的所有文件
    for root, _, files in os.walk(folder_path):
        for file in files:
            if file.endswith('.txt'):
                file_path = os.path.join(root, file)
                items_count = process_file(file_path)
                if items_count > 0:
                    # 获取不带扩展名的文件名
                    filename_without_ext = get_filename_without_extension(file_path)
                    # 保存文件名和对应的数字
                    matching_files.append((filename_without_ext, items_count))
                    print(f"找到匹配文件: {filename_without_ext}, 数字: {items_count}")
    
    # 保存结果到脚本所在目录
    script_dir = os.path.dirname(os.path.abspath(__file__))
    output_path = os.path.join(script_dir, "NonModifiableTextures Shaders.txt")
    
    with open(output_path, 'w', encoding='utf-8') as f:
        for filename, items_count in matching_files:
            # 使用f-string格式化输出[7](@ref)
            f.write(f"{filename} {items_count}\n")
    
    print(f"处理完成！找到 {len(matching_files)} 个匹配文件。")
    print(f"结果已保存到: {output_path}")

if __name__ == "__main__":
    main()
