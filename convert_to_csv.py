import pandas as pd

# Read the Excel file
excel_file = r'C:\POCs\IEXInsider\MCP and MCV Details (Jan-2023 to Sept-2025)-Divyesh.xlsx'
df = pd.read_excel(excel_file, sheet_name='MCP Details')

# Clean column names
df.columns = df.columns.str.strip()

# Save to CSV
csv_file = r'C:\POCs\IEXInsider\IEX_Market_Data.csv'
df.to_csv(csv_file, index=False)

print(f"Successfully converted to CSV: {csv_file}")
print(f"Total rows: {len(df)}")
print(f"Columns: {list(df.columns)}")
