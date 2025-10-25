import pandas as pd

# Read the Excel file
df = pd.read_excel(r'C:\POCs\IEXInsider\MCP and MCV Details (Jan-2023 to Sept-2025)-Divyesh.xlsx', sheet_name='MCP Details')

# Clean column names
df.columns = df.columns.str.strip()

print('Cleaned Columns:', list(df.columns))
print('\n' + '='*80 + '\n')

# Check TYPE values
print('TYPE values:')
print(df['TYPE'].value_counts())
print('\n' + '='*80 + '\n')

# Sample data for each TYPE
for type_val in df['TYPE'].unique():
    print(f'Sample data for TYPE = {type_val}:')
    sample = df[df['TYPE'] == type_val].head(10)
    print(sample[['TYPE', 'Year', 'Date', 'Time_Block', 'IEX_Demand (GW)', 'IEX_Supply (GW)', 'MCP (Rs./kWh)', 'MCV (GW)']].to_string())
    print('\n' + '='*80 + '\n')

# Date range
print(f'Date range: {df["Date"].min()} to {df["Date"].max()}')
print(f'Total records: {len(df)}')
print(f'Years: {sorted(df["Year"].unique())}')

# Check for null values in important columns
print('\nNull values in key columns:')
print(df[['TYPE', 'Year', 'Date', 'Time_Block', 'IEX_Demand (GW)', 'IEX_Supply (GW)', 'MCP (Rs./kWh)', 'MCV (GW)']].isnull().sum())

# Price statistics
print('\nMCP Price Statistics (Rs./kWh):')
print(df['MCP (Rs./kWh)'].describe())

print('\nMCV Volume Statistics (GW):')
print(df['MCV (GW)'].describe())
