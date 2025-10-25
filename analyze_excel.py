import pandas as pd
import sys

# Read the Excel file
xl = pd.ExcelFile(r'C:\POCs\IEXInsider\MCP and MCV Details (Jan-2023 to Sept-2025)-Divyesh.xlsx')
print('Sheet names:', xl.sheet_names)
print('\n' + '='*80 + '\n')

# Analyze each sheet
for sheet_name in xl.sheet_names:
    print(f'SHEET: {sheet_name}')
    print('='*80)
    df = pd.read_excel(xl, sheet_name)

    print(f'\nColumns: {list(df.columns)}')
    print(f'\nShape: {df.shape} (rows, columns)')

    print('\nData types:')
    print(df.dtypes)

    print('\nFirst 5 rows:')
    print(df.head(5).to_string())

    print('\nUnique values per column:')
    for col in df.columns:
        print(f'  {col}: {df[col].nunique()} unique values')

    print('\nBasic statistics:')
    print(df.describe())

    print('\n' + '='*80 + '\n')
