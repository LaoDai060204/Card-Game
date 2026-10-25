import os
import sys
import re

def extract_number_from_line(line):
    """
    Extract the number before items from a line.
    Example input: "  0 Array Array (1 items)"
    Returns: 1
    """
    match = re.search(r'\((\d+)\s+items\)', line)
    if match:
        return int(match.group(1))
    return 0

def process_file(file_path):
    """
    Process a file, finding m_NonModifiableTextures and the following line.
    Returns: The positive count, or zero when no match is found.
    """
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.readlines()
    except Exception as e:
        print(f"Cannot read file {file_path}: {e}")
        return 0

    # Scan backward because the target line is near the end of the file.
    for i in range(len(content) - 2, -1, -1):
        if "m_NonModifiableTextures" in content[i]:
            # Check whether the next line contains items.
            if i + 1 < len(content) and "items" in content[i + 1]:
                items_count = extract_number_from_line(content[i + 1])
                if items_count > 0:
                    return items_count
    return 0

def get_filename_without_extension(file_path):
    """
    Extract the filename without its extension from a path.
    """
    # Get the filename with its extension.
    filename_with_ext = os.path.basename(file_path)
    # Remove the extension.
    filename_without_ext = os.path.splitext(filename_with_ext)[0]
    return filename_without_ext

def main():
    if len(sys.argv) < 2:
        input("Drag a folder onto this script, then press Enter to continue...")
        return
    
    folder_path = sys.argv[1]
    
    if not os.path.isdir(folder_path):
        print("Error: the supplied path is not a folder")
        return
    
    print(f"Processing folder: {folder_path}")
    
    # Store matching filenames and their associated counts.
    matching_files = []
    
    # Visit every file in the folder.
    for root, _, files in os.walk(folder_path):
        for file in files:
            if file.endswith('.txt'):
                file_path = os.path.join(root, file)
                items_count = process_file(file_path)
                if items_count > 0:
                    # Get the filename without its extension.
                    filename_without_ext = get_filename_without_extension(file_path)
                    # Save the filename and its associated count.
                    matching_files.append((filename_without_ext, items_count))
                    print(f"Matching file: {filename_without_ext}, Count: {items_count}")
    
    # Save results in the script directory.
    script_dir = os.path.dirname(os.path.abspath(__file__))
    output_path = os.path.join(script_dir, "NonModifiableTextures Shaders.txt")
    
    with open(output_path, 'w', encoding='utf-8') as f:
        for filename, items_count in matching_files:
            # Format the output with an f-string.
            f.write(f"{filename} {items_count}\n")
    
    print(f"Processing complete. Matches found: {len(matching_files)} matching files.")
    print(f"Results saved to: {output_path}")

if __name__ == "__main__":
    main()
