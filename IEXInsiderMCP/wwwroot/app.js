// API Base URL
const API_BASE = window.location.origin;

// Speech Recognition
let recognition = null;
let isRecording = false;
let chartInstances = {};

// Helper function to get market-specific colors matching theme
function getMarketColor(market) {
    const colors = {
        'DAM': '#0f4c81',   // Primary Blue (matches header)
        'GDAM': '#00a8cc',  // Accent Cyan (matches header)
        'RTM': '#14b8a6'    // Teal (complementary)
    };
    return colors[market] || '#64748b';  // Professional Slate Gray
}

// Load and display suggested queries
async function loadSuggestedQueries() {
    try {
        const response = await fetch('/api/query/suggestions');
        if (!response.ok) throw new Error('Failed to load suggestions');

        const data = await response.json();
        displaySuggestedQueries(data.categories);
    } catch (error) {
        console.error('Error loading suggested queries:', error);
    }
}

function displaySuggestedQueries(categories) {
    const suggestionsContainer = document.getElementById('suggestions');
    if (!suggestionsContainer || !categories || categories.length === 0) return;

    // Create toggle button
    const toggleButton = document.createElement('div');
    toggleButton.className = 'suggestions-toggle';
    toggleButton.innerHTML = `
        <span>💡 Explore Sample Queries</span>
        <span class="icon">▼</span>
    `;

    // Create categories container
    const categoriesContainer = document.createElement('div');
    categoriesContainer.className = 'suggestions-categories';
    categoriesContainer.style.display = 'none'; // Hidden by default

    // Populate categories
    categories.forEach(category => {
        const categoryDiv = document.createElement('div');
        categoryDiv.className = 'suggestion-category';

        const header = document.createElement('div');
        header.className = 'category-header';
        header.innerHTML = `
            <div class="category-icon">${category.icon}</div>
            <div class="category-info">
                <h4 class="category-name">${category.name}</h4>
                <p class="category-description">${category.description}</p>
            </div>
            <div class="category-toggle">▼</div>
        `;

        const queriesDiv = document.createElement('div');
        queriesDiv.className = 'category-queries';
        queriesDiv.style.display = 'none'; // Start collapsed

        category.queries.forEach(query => {
            const queryItem = document.createElement('div');
            queryItem.className = 'query-item';
            queryItem.textContent = query;
            queryItem.addEventListener('click', () => {
                const queryInput = document.getElementById('queryInput');
                if (queryInput) {
                    queryInput.value = query;
                    queryInput.focus();
                    // Hide suggestions after selection
                    categoriesContainer.style.display = 'none';
                    toggleButton.classList.add('collapsed');
                }
            });
            queriesDiv.appendChild(queryItem);
        });

        // Category header click to expand/collapse
        header.style.cursor = 'pointer';
        header.addEventListener('click', () => {
            const isHidden = queriesDiv.style.display === 'none';
            queriesDiv.style.display = isHidden ? 'flex' : 'none';
            const toggleIcon = header.querySelector('.category-toggle');
            if (toggleIcon) {
                toggleIcon.textContent = isHidden ? '▲' : '▼';
            }
            categoryDiv.classList.toggle('expanded', isHidden);
        });

        categoryDiv.appendChild(header);
        categoryDiv.appendChild(queriesDiv);
        categoriesContainer.appendChild(categoryDiv);
    });

    // Toggle functionality
    toggleButton.addEventListener('click', () => {
        const isHidden = categoriesContainer.style.display === 'none';
        categoriesContainer.style.display = isHidden ? 'grid' : 'none';
        toggleButton.classList.toggle('collapsed', !isHidden);
    });

    // Append to container
    suggestionsContainer.innerHTML = '';
    suggestionsContainer.appendChild(toggleButton);
    suggestionsContainer.appendChild(categoriesContainer);
}

// Initialize on load
document.addEventListener('DOMContentLoaded', () => {
    initSpeechRecognition();
    setupEventListeners();
    loadInitialStats();
    adjustTextareaHeight();
    loadSuggestedQueries();
});

// Setup Event Listeners
function setupEventListeners() {
    // Send button
    document.getElementById('sendButton').addEventListener('click', sendMessage);

    // Enter key (Shift+Enter for new line)
    document.getElementById('queryInput').addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    // Auto-resize textarea
    document.getElementById('queryInput').addEventListener('input', adjustTextareaHeight);

    // Microphone button
    document.getElementById('micButton').addEventListener('click', toggleVoiceInput);

    // Example cards
    document.querySelectorAll('.example-card').forEach(card => {
        card.addEventListener('click', () => {
            const query = card.getAttribute('data-query');
            document.getElementById('queryInput').value = query;
            sendMessage();
        });
    });
}

// Adjust textarea height
function adjustTextareaHeight() {
    const textarea = document.getElementById('queryInput');
    textarea.style.height = 'auto';
    textarea.style.height = Math.min(textarea.scrollHeight, 200) + 'px';
}

// Initialize Speech Recognition
function initSpeechRecognition() {
    if ('webkitSpeechRecognition' in window || 'SpeechRecognition' in window) {
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        recognition = new SpeechRecognition();
        recognition.continuous = false;
        recognition.interimResults = false;
        recognition.lang = 'en-IN';

        recognition.onstart = () => {
            isRecording = true;
            document.getElementById('micButton').classList.add('recording');
        };

        recognition.onresult = (event) => {
            const transcript = event.results[0][0].transcript;
            document.getElementById('queryInput').value = transcript;
            adjustTextareaHeight();
        };

        recognition.onerror = (event) => {
            console.error('Speech recognition error:', event.error);
            stopRecording();
        };

        recognition.onend = () => {
            stopRecording();
        };
    }
}

function toggleVoiceInput() {
    if (!recognition) {
        addAssistantMessage('Speech recognition is not supported in this browser. Please use Chrome, Edge, or Safari.');
        return;
    }

    if (isRecording) {
        recognition.stop();
    } else {
        recognition.start();
    }
}

function stopRecording() {
    isRecording = false;
    document.getElementById('micButton').classList.remove('recording');
}

// Load Initial Stats
async function loadInitialStats() {
    try {
        const response = await fetch(`${API_BASE}/api/iex/statistics`);
        const stats = await response.json();

        if (stats.TotalRecords) {
            const dateRange = stats.DateRange;
            const startYear = new Date(dateRange.Start).getFullYear();
            const endYear = new Date(dateRange.End).getFullYear();

            document.getElementById('headerStats').textContent =
                `${stats.TotalRecords.toLocaleString()} Records | ${stats.MarketTypes.length} Markets | ${startYear}-${endYear}`;
        }
    } catch (error) {
        console.error('Error loading stats:', error);
    }
}

// Parse Query Intent - Convert natural language to structured filters
function parseQueryIntent(query) {
    const queryLower = query.toLowerCase();
    const params = {
        query: query,
        filters: {},
        limit: 100
    };

    // Extract year
    const yearMatch = query.match(/\b(202[3-9]|203[0-9])\b/);
    if (yearMatch) {
        params.filters.year = parseInt(yearMatch[1]);
    }

    // Extract market type
    if (queryLower.includes('dam') && !queryLower.includes('gdam')) {
        params.filters.market_type = 'DAM';
    } else if (queryLower.includes('gdam')) {
        params.filters.market_type = 'GDAM';
    } else if (queryLower.includes('rtm')) {
        params.filters.market_type = 'RTM';
    }

    // Extract price range (e.g., "9-10 Rs", "within 9 to 10", "between 9 and 10")
    const priceRangeMatch = query.match(/(\d+(?:\.\d+)?)\s*(?:-|to|and)\s*(\d+(?:\.\d+)?)\s*(?:rs|rupees|₹)?/i);
    if (priceRangeMatch) {
        const min = parseFloat(priceRangeMatch[1]);
        const max = parseFloat(priceRangeMatch[2]);
        params.filters.mcp_min = Math.min(min, max);
        params.filters.mcp_max = Math.max(min, max);
    }

    // Extract time block range (e.g., "5pm to 9pm", "17:00 to 21:00", "5:00pm-9:00pm")
    const timeRangeMatch = query.match(/(\d{1,2})(?::(\d{2}))?\s*(?:am|pm)?\s*(?:-|to)\s*(\d{1,2})(?::(\d{2}))?\s*(am|pm)?/i);
    if (timeRangeMatch) {
        let startHour = parseInt(timeRangeMatch[1]);
        let endHour = parseInt(timeRangeMatch[3]);
        const endPeriod = timeRangeMatch[5]?.toLowerCase();

        // Convert to 24-hour format
        if (endPeriod === 'pm' && endHour < 12) {
            endHour += 12;
        }
        if (queryLower.includes('pm') && startHour < 12 && startHour < endHour) {
            startHour += 12;
        }

        params.filters.time_block_start = `${String(startHour).padStart(2, '0')}:00`;
        params.filters.time_block_end = `${String(endHour).padStart(2, '0')}:00`;
    }

    // Detect heat map intent (special case - uses different endpoint)
    if (queryLower.includes('heat map') || queryLower.includes('heatmap') || queryLower.includes('heat-map')) {
        params.isHeatMap = true;
        return params; // Return early for heat map
    }

    // Detect aggregation intent
    if (queryLower.includes('average') || queryLower.includes('avg') || queryLower.includes('mean')) {
        params.aggregation = 'average';
    } else if (queryLower.includes('highest') || queryLower.includes('maximum') || queryLower.includes('max') || queryLower.includes('peak')) {
        params.aggregation = 'max';
    } else if (queryLower.includes('lowest') || queryLower.includes('minimum') || queryLower.includes('min')) {
        params.aggregation = 'min';
    } else if (queryLower.includes('total') || queryLower.includes('sum')) {
        params.aggregation = 'sum';
    } else if (queryLower.includes('count') || queryLower.includes('how many') || queryLower.includes('number of')) {
        params.aggregation = 'count';
    } else if (queryLower.includes('standard deviation') || queryLower.includes('stddev') || queryLower.includes('std dev')) {
        params.aggregation = 'stddev';
    }

    // Detect grouping intent
    if (queryLower.includes('each month') || queryLower.includes('by month') || queryLower.includes('monthly') || queryLower.includes('which month')) {
        params.group_by = 'month';
    } else if (queryLower.includes('each year') || queryLower.includes('by year') || queryLower.includes('yearly')) {
        params.group_by = 'year';
    } else if (queryLower.includes('each market') || queryLower.includes('by market') || queryLower.includes('compare market')) {
        params.group_by = 'market_type';
    } else if (queryLower.includes('each day') || queryLower.includes('by day') || queryLower.includes('daily')) {
        params.group_by = 'date';
    } else if (queryLower.includes('each hour') || queryLower.includes('by hour') || queryLower.includes('hourly')) {
        params.group_by = 'hour';
    } else if (queryLower.includes('time block') && (queryLower.includes('each') || queryLower.includes('by'))) {
        params.group_by = 'time_block';
    }

    // If grouping is detected but no aggregation specified, default to average
    if (params.group_by && !params.aggregation) {
        params.aggregation = 'average';
    }

    // Remove limit for aggregation queries
    if (params.aggregation || params.group_by) {
        delete params.limit;
    }

    return params;
}

// Handle Heat Map Request - INTELLIGENT VERSION
async function handleHeatMapRequest(query, queryParams) {
    hideTypingIndicator();

    const queryLower = query.toLowerCase();

    // Determine which metrics to show (MCP, MCV, or both)
    const hasMCP = queryLower.includes('mcp');
    const hasMCV = queryLower.includes('mcv');

    // If both mentioned, generate two heat maps
    // If neither mentioned, default to MCP
    // If only one mentioned, show that one
    const metrics = [];
    if (hasMCP && hasMCV) {
        metrics.push('mcp', 'mcv');
    } else if (hasMCV) {
        metrics.push('mcv');
    } else {
        metrics.push('mcp');
    }

    try {
        // Use the NEW intelligent POST endpoint that parses the entire query
        const heatMapPromises = metrics.map(async (metric) => {
            // Modify query to specify metric
            let metricQuery = query;
            if (metrics.length === 1) {
                // If only one metric, use the query as-is
                metricQuery = query.replace(/\b(mcp|mcv)\b/gi, metric.toUpperCase());
            } else {
                // If both, create separate queries
                metricQuery = query.replace(/\b(mcp|mcv)\b/gi, metric.toUpperCase());
            }

            const url = `${API_BASE}/api/iex/heatmap`;
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ query: metricQuery })
            });

            const data = await response.json();
            return { metric, data };
        });

        const results = await Promise.all(heatMapPromises);

        // Check for errors
        const failedResults = results.filter(r => !r.data.success);
        if (failedResults.length > 0) {
            addAssistantMessage(`❌ ${failedResults[0].data.message}`);
            return;
        }

        // Display all heat maps
        results.forEach(({ metric, data }) => {
            displayHeatMap(query, data, metric);
        });

    } catch (error) {
        console.error('Heat map error:', error);
        addAssistantMessage(`❌ Error generating heat map: ${error.message}`);
    } finally {
        document.getElementById('sendButton').disabled = false;
    }
}

// Handle Multi-Time-Slot Request
async function handleMultiTimeSlotRequest(query) {
    hideTypingIndicator();

    try {
        const url = `${API_BASE}/api/iex/multi-timeslot`;
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ query: query })
        });

        const data = await response.json();

        if (!data.success) {
            addAssistantMessage(`❌ ${data.message}`);
            return;
        }

        // Display overview message
        addAssistantMessage(`### 📊 Multi-Time-Slot Analysis\n\n**${data.message}**\n\nGenerated ${data.results.length} results across markets and time slots.`);

        // Group results by time slot for better organization
        const resultsByTimeSlot = {};
        data.results.forEach(result => {
            if (!resultsByTimeSlot[result.timeSlotName]) {
                resultsByTimeSlot[result.timeSlotName] = [];
            }
            resultsByTimeSlot[result.timeSlotName].push(result);
        });

        // Display results grouped by time slot
        for (const [timeSlotName, results] of Object.entries(resultsByTimeSlot)) {
            displayMultiTimeSlotGroup(timeSlotName, results, query);
        }

    } catch (error) {
        console.error('Multi-time-slot error:', error);
        addAssistantMessage(`❌ Error generating multi-time-slot analysis: ${error.message}`);
    } finally {
        document.getElementById('sendButton').disabled = false;
    }
}

// Display a group of results for a single time slot across markets
function displayMultiTimeSlotGroup(timeSlotName, results, query) {
    const queryLower = query.toLowerCase();

    // Check if this is a combined MCP+MCV chart
    const hasMCV = queryLower.includes('mcv') || queryLower.includes('volume');
    const hasMCP = queryLower.includes('mcp') || queryLower.includes('price');
    const isCombinedChart = hasMCV && hasMCP;

    // Determine metric type
    let metricType = 'MCP';
    if (isCombinedChart) {
        metricType = 'MCP & MCV';
    } else if (hasMCV) {
        metricType = 'MCV';
    }
    const unit = metricType === 'MCV' ? 'GW' : (metricType === 'MCP & MCV' ? 'Mixed' : '₹/kWh');

    // Determine chart type
    let chartType = 'heatmap';
    if (queryLower.includes('bar chart') || queryLower.includes('bar graph')) {
        chartType = 'bar';
    } else if (queryLower.includes('line chart') || queryLower.includes('line graph')) {
        chartType = 'line';
    }

    let content = `### 🕐 Time Slot: ${timeSlotName}\n\n`;
    addAssistantMessage(content);

    // Create a styled HTML table for statistics
    const tableContainer = document.createElement('div');
    tableContainer.style.margin = '20px 0';
    tableContainer.style.overflowX = 'auto';

    const table = document.createElement('table');
    table.className = 'stats-table';

    // Check if this is a combined multi-market result
    const isCombinedMultiMarket = results.length > 0 && results[0].market === 'All Markets';

    // Create header
    const thead = document.createElement('thead');
    const headerRow = document.createElement('tr');
    if (isCombinedMultiMarket) {
        ['Metric', 'Average', 'Max', 'Min', 'Records'].forEach(header => {
            const th = document.createElement('th');
            th.textContent = header;
            headerRow.appendChild(th);
        });
    } else {
        ['Market', 'Average', 'Max', 'Min', 'Records'].forEach(header => {
            const th = document.createElement('th');
            th.textContent = header;
            headerRow.appendChild(th);
        });
    }
    thead.appendChild(headerRow);
    table.appendChild(thead);

    // Create body
    const tbody = document.createElement('tbody');
    results.forEach(result => {
        const stats = result.statistics;

        if (isCombinedMultiMarket) {
            // For combined charts, show MCP and MCV rows separately
            // MCP row
            const mcpRow = document.createElement('tr');
            const mcpLabelCell = document.createElement('td');
            mcpLabelCell.innerHTML = `<strong style="color: #0f4c81">MCP (Price)</strong>`;
            mcpRow.appendChild(mcpLabelCell);

            const mcpAvgCell = document.createElement('td');
            mcpAvgCell.textContent = `${(stats.avg_mcp || 0).toFixed(2)} ₹/kWh`;
            mcpAvgCell.style.textAlign = 'right';
            mcpRow.appendChild(mcpAvgCell);

            const mcpMaxCell = document.createElement('td');
            mcpMaxCell.textContent = `${(stats.max_mcp || 0).toFixed(2)} ₹/kWh`;
            mcpMaxCell.style.textAlign = 'right';
            mcpMaxCell.style.color = '#e74c3c';
            mcpMaxCell.style.fontWeight = 'bold';
            mcpRow.appendChild(mcpMaxCell);

            const mcpMinCell = document.createElement('td');
            mcpMinCell.textContent = `${(stats.min_mcp || 0).toFixed(2)} ₹/kWh`;
            mcpMinCell.style.textAlign = 'right';
            mcpMinCell.style.color = '#27ae60';
            mcpMinCell.style.fontWeight = 'bold';
            mcpRow.appendChild(mcpMinCell);

            const mcpRecordsCell = document.createElement('td');
            mcpRecordsCell.textContent = result.recordCount.toLocaleString();
            mcpRecordsCell.style.textAlign = 'right';
            mcpRecordsCell.rowSpan = 2;
            mcpRow.appendChild(mcpRecordsCell);

            tbody.appendChild(mcpRow);

            // MCV row
            const mcvRow = document.createElement('tr');
            const mcvLabelCell = document.createElement('td');
            mcvLabelCell.innerHTML = `<strong style="color: #00a8cc">MCV (Volume)</strong>`;
            mcvRow.appendChild(mcvLabelCell);

            const mcvAvgCell = document.createElement('td');
            mcvAvgCell.textContent = `${(stats.avg_mcv || 0).toFixed(2)} GW`;
            mcvAvgCell.style.textAlign = 'right';
            mcvRow.appendChild(mcvAvgCell);

            const mcvMaxCell = document.createElement('td');
            mcvMaxCell.textContent = `${(stats.max_mcv || 0).toFixed(2)} GW`;
            mcvMaxCell.style.textAlign = 'right';
            mcvMaxCell.style.color = '#e74c3c';
            mcvMaxCell.style.fontWeight = 'bold';
            mcvRow.appendChild(mcvMaxCell);

            const mcvMinCell = document.createElement('td');
            mcvMinCell.textContent = `${(stats.min_mcv || 0).toFixed(2)} GW`;
            mcvMinCell.style.textAlign = 'right';
            mcvMinCell.style.color = '#27ae60';
            mcvMinCell.style.fontWeight = 'bold';
            mcvRow.appendChild(mcvMinCell);

            tbody.appendChild(mcvRow);
        } else {
            // For single-market charts, show normal row
            const row = document.createElement('tr');

            // Market name cell with color indicator
            const marketCell = document.createElement('td');
            marketCell.innerHTML = `<strong style="color: ${getMarketColor(result.market)}">${result.market}</strong>`;
            row.appendChild(marketCell);

            // Statistics cells
            const avgCell = document.createElement('td');
            avgCell.textContent = `${(stats.average || 0).toFixed(2)} ${unit}`;
            avgCell.style.textAlign = 'right';
            row.appendChild(avgCell);

            const maxCell = document.createElement('td');
            maxCell.textContent = `${(stats.max || 0).toFixed(2)} ${unit}`;
            maxCell.style.textAlign = 'right';
            maxCell.style.color = '#e74c3c';
            maxCell.style.fontWeight = 'bold';
            row.appendChild(maxCell);

            const minCell = document.createElement('td');
            minCell.textContent = `${(stats.min || 0).toFixed(2)} ${unit}`;
            minCell.style.textAlign = 'right';
            minCell.style.color = '#27ae60';
            minCell.style.fontWeight = 'bold';
            row.appendChild(minCell);

            const recordsCell = document.createElement('td');
            recordsCell.textContent = result.recordCount.toLocaleString();
            recordsCell.style.textAlign = 'right';
            row.appendChild(recordsCell);

            tbody.appendChild(row);
        }
    });
    table.appendChild(tbody);
    tableContainer.appendChild(table);

    // Append to the last assistant message's text container
    const messagesArea = document.getElementById('messagesArea');
    const lastMessage = messagesArea.lastElementChild;
    if (lastMessage) {
        const messageText = lastMessage.querySelector('.message-text');
        if (messageText) {
            messageText.appendChild(tableContainer);
        }
    }

    // Render visualizations for each market in this time slot
    results.forEach(result => {
        if (chartType === 'heatmap' && result.chartData) {
            displayHeatMapForTimeSlot(result, metricType, unit, timeSlotName);
        } else if ((chartType === 'bar' || chartType === 'line') && result.chartData) {
            displayChartForTimeSlot(result, metricType, unit, timeSlotName, chartType);
        }
    });
}

// Display heat map for a specific time slot and market
function displayHeatMapForTimeSlot(result, metricType, unit, timeSlotName) {
    const heatMapData = result.chartData;

    // Check if there's any data
    if (!heatMapData || !heatMapData.dates || heatMapData.dates.length === 0 || result.recordCount === 0) {
        let content = `#### 🔥 ${result.market} - ${metricType} Heat Map\n\n`;
        content += `> ⚠️ **No data available** for ${result.market} in time slot ${timeSlotName}\n\n`;
        addAssistantMessage(content);
        return;
    }

    // Determine grouping info for title
    const groupingUnit = heatMapData.grouping_unit || 'day';
    const daysRange = heatMapData.days_range || 0;
    const groupingText = groupingUnit === 'week' ? ' (grouped by week)' :
                        groupingUnit === 'month' ? ' (grouped by month)' : '';

    let content = `#### 🔥 ${result.market} - ${metricType} Heat Map\n\n`;

    if (daysRange > 0) {
        content += `> 📅 Date range: ${daysRange} days${groupingText}\n\n`;
    }

    // Add canvas for heat map visualization (25% larger for better visibility)
    const chartId = `heatmap-${result.market}-${timeSlotName}-${Date.now()}`;
    content += `<div class="chart-container">
        <div class="chart-header">
            <div class="chart-title">${result.market}: ${metricType} by Time Block (${timeSlotName})</div>
            <div class="chart-actions">
                <button class="chart-action-btn" onclick="downloadChart('${chartId}')" title="Download Chart">📥</button>
            </div>
        </div>
        <canvas id="${chartId}" style="max-height: 625px; width: 100%;"></canvas>
    </div>`;

    addAssistantMessage(content);

    // Render heat map after DOM update
    setTimeout(() => {
        renderHeatMap(chartId, heatMapData, metricType, unit);
    }, 100);
}

// Display bar/line chart for a specific time slot and market
function displayChartForTimeSlot(result, metricType, unit, timeSlotName, chartType) {
    const chartData = result.chartData;

    // Check if there's any data
    if (!chartData || !chartData.labels || chartData.labels.length === 0 || result.recordCount === 0) {
        let content = `#### 📊 ${result.market} - ${metricType} ${chartType === 'bar' ? 'Bar Chart' : 'Line Graph'}\n\n`;
        content += `> ⚠️ **No data available** for ${result.market} in time slot ${timeSlotName}\n\n`;
        addAssistantMessage(content);
        return;
    }

    // Determine grouping info
    const groupingUnit = chartData.grouping_unit || 'day';
    const daysRange = chartData.days_range || 0;
    const groupingText = groupingUnit === 'week' ? ' (grouped by week)' :
                        groupingUnit === 'month' ? ' (grouped by month)' : '';

    let content = `#### 📊 ${result.market} - ${metricType} ${chartType === 'bar' ? 'Bar Chart' : 'Line Graph'}\n\n`;

    if (daysRange > 0) {
        content += `> 📅 Date range: ${daysRange} days${groupingText}\n\n`;
    }

    // Add canvas for chart visualization (25% larger for better visibility)
    const chartId = `chart-${result.market}-${timeSlotName}-${Date.now()}`;
    content += `<div class="chart-container">
        <div class="chart-header">
            <div class="chart-title">${result.market}: ${metricType} Over Time (${timeSlotName})</div>
            <div class="chart-actions">
                <button class="chart-action-btn" onclick="downloadChart('${chartId}')" title="Download Chart">📥</button>
            </div>
        </div>
        <canvas id="${chartId}" style="max-height: 500px; width: 100%;"></canvas>
    </div>`;

    addAssistantMessage(content);

    // Render chart after DOM update
    setTimeout(() => {
        renderTimeSlotChart(chartId, chartData, metricType, unit, chartType);
    }, 100);
}

// Render bar/line chart for time slot data
function renderTimeSlotChart(chartId, chartData, metricType, unit, chartType) {
    const ctx = document.getElementById(chartId);
    if (!ctx) return;

    // Destroy existing chart if any
    if (chartInstances[chartId]) {
        chartInstances[chartId].destroy();
    }

    // Check if this is a combined chart with dual Y-axes (check both camelCase and snake_case)
    const hasDualAxes = chartData.hasDualAxes === true || chartData.has_dual_axes === true || chartData.chartType === 'combined_bar_line';
    const isCombinedChart = hasDualAxes && chartData.datasets && chartData.datasets.length > 1;

    let datasets;
    let scales;

    if (isCombinedChart) {
        // Use datasets from backend for combined charts
        datasets = chartData.datasets.map((dataset, idx) => ({
            label: dataset.label,
            data: dataset.data,
            type: dataset.type || 'bar',
            backgroundColor: dataset.backgroundColor,
            borderColor: dataset.borderColor,
            borderWidth: dataset.borderWidth || 2,
            fill: dataset.fill !== undefined ? dataset.fill : false,
            tension: dataset.tension || 0.4,
            yAxisID: dataset.yAxisID,
            order: dataset.order || 1,
            pointRadius: dataset.type === 'line' ? 4 : 0,
            pointHoverRadius: dataset.type === 'line' ? 6 : 0,
            pointBackgroundColor: dataset.borderColor,
            pointBorderColor: '#fff',
            pointBorderWidth: 2
        }));

        // Dual Y-axes configuration
        scales = {
            x: {
                title: {
                    display: true,
                    text: 'Date',
                    padding: { top: 10, bottom: 0 }
                },
                ticks: {
                    maxRotation: 45,
                    minRotation: 45,
                    autoSkip: true,
                    maxTicksLimit: 20,
                    padding: 5
                },
                grid: {
                    drawOnChartArea: true,
                    offset: false
                }
            },
            'y-mcv': {
                type: 'linear',
                position: 'left',
                beginAtZero: true,
                title: {
                    display: true,
                    text: 'Volume (GW)',
                    color: '#00a8cc',
                    padding: { bottom: 10 }
                },
                ticks: {
                    color: '#00a8cc',
                    callback: function(value) {
                        return value.toFixed(2);
                    }
                },
                grid: {
                    drawOnChartArea: true
                }
            },
            'y-mcp': {
                type: 'linear',
                position: 'right',
                beginAtZero: true,
                title: {
                    display: true,
                    text: 'Price (₹/kWh)',
                    color: '#0f4c81',
                    padding: { bottom: 10 }
                },
                ticks: {
                    color: '#0f4c81',
                    callback: function(value) {
                        return value.toFixed(2);
                    }
                },
                grid: {
                    drawOnChartArea: false
                }
            }
        };
    } else {
        // Single metric chart
        const colors = {
            primary: 'rgba(102, 126, 234, 0.8)',
            primaryBorder: 'rgba(102, 126, 234, 1)'
        };

        datasets = [{
            label: `${metricType} (${unit})`,
            data: chartData.values || [],
            backgroundColor: chartType === 'bar' ? colors.primary : 'rgba(102, 126, 234, 0.1)',
            borderColor: colors.primaryBorder,
            borderWidth: 2,
            fill: chartType === 'line',
            tension: 0.4
        }];

        scales = {
            x: {
                title: {
                    display: true,
                    text: 'Date',
                    padding: { top: 10, bottom: 0 }
                },
                ticks: {
                    maxRotation: 45,
                    minRotation: 45,
                    autoSkip: true,
                    maxTicksLimit: 20,
                    padding: 5
                },
                grid: {
                    drawOnChartArea: true,
                    offset: false
                }
            },
            y: {
                beginAtZero: true,
                title: {
                    display: true,
                    text: metricType === 'MCV' ? 'Volume (GW)' : 'Price (₹/kWh)',
                    padding: { bottom: 10 }
                },
                ticks: {
                    callback: function(value) {
                        return value.toFixed(2);
                    }
                }
            }
        };
    }

    chartInstances[chartId] = new Chart(ctx, {
        type: isCombinedChart ? 'bar' : chartType,
        data: {
            labels: chartData.labels || [],
            datasets: datasets
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            layout: {
                padding: {
                    left: 10,
                    right: 20,
                    top: 10,
                    bottom: 10
                }
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                    labels: {
                        padding: 10
                    }
                },
                tooltip: {
                    callbacks: {
                        label: function(context) {
                            return `${context.dataset.label}: ${context.parsed.y.toFixed(2)}`;
                        }
                    }
                }
            },
            scales: scales
        }
    });
}

// Display Heat Map
function displayHeatMap(query, heatMapData, metric) {
    const metricName = metric.toUpperCase();
    const unit = metric === 'mcv' ? 'GW' : '₹/kWh';

    let content = `### 🔥 Heat Map: ${metricName}\n\n`;
    content += `**${heatMapData.message}**\n\n`;

    // Create info card with extracted context
    content += `<div class="heatmap-info">`;
    content += `<p><strong>Metric:</strong> ${metricName} (${unit})</p>`;
    content += `<p><strong>Time Period:</strong> ${heatMapData.time_period_start} to ${heatMapData.time_period_end}</p>`;
    if (heatMapData.markets && heatMapData.markets.length > 0) {
        content += `<p><strong>Markets:</strong> ${heatMapData.markets.join(', ')}</p>`;
    }
    if (heatMapData.extracted_filters && Object.keys(heatMapData.extracted_filters).length > 0) {
        content += `<p><strong>Filters Applied:</strong> ${Object.entries(heatMapData.extracted_filters).map(([k, v]) => `${k}=${v}`).join(', ')}</p>`;
    }
    content += `<p><strong>Days:</strong> ${heatMapData.dates.length}</p>`;
    content += `<p><strong>Time Blocks per Day:</strong> ${heatMapData.time_blocks.length}</p>`;
    content += `</div>\n\n`;

    // Calculate statistics from the matrix
    let allValues = [];
    heatMapData.matrix.forEach(row => {
        row.forEach(val => {
            if (val !== null) allValues.push(val);
        });
    });

    if (allValues.length > 0) {
        const min = Math.min(...allValues);
        const max = Math.max(...allValues);
        const avg = allValues.reduce((a, b) => a + b, 0) / allValues.length;

        content += `**Statistics:**\n`;
        content += `- Maximum: ${max.toFixed(2)} ${unit}\n`;
        content += `- Average: ${avg.toFixed(2)} ${unit}\n`;
        content += `- Minimum: ${min.toFixed(2)} ${unit}\n`;
        content += `- Data Points: ${allValues.length.toLocaleString()}\n\n`;
    }

    // Add canvas for heat map visualization (25% larger for better visibility)
    const chartId = 'heatmap-' + Date.now();
    content += `<div class="chart-container">
        <div class="chart-header">
            <div class="chart-title">Heat Map: ${metricName} by Time Block and Date</div>
            <div class="chart-actions">
                <button class="chart-action-btn" onclick="downloadChart('${chartId}')" title="Download Chart">📥</button>
            </div>
        </div>
        <canvas id="${chartId}" style="max-height: 750px; width: 100%;"></canvas>
    </div>`;

    addAssistantMessage(content);

    // Render heat map after DOM update
    setTimeout(() => {
        renderHeatMap(chartId, heatMapData, metricName, unit);
    }, 100);
}

// Render Heat Map Chart
function renderHeatMap(chartId, heatMapData, metricName, unit) {
    const ctx = document.getElementById(chartId);
    if (!ctx) return;

    // Destroy existing chart if any
    if (chartInstances[chartId]) {
        chartInstances[chartId].destroy();
    }

    // Prepare data for heat map (showing hourly aggregations for readability)
    const dates = heatMapData.dates;
    const matrix = heatMapData.matrix;
    const timeBlocks = heatMapData.time_blocks;

    // Aggregate by hour (every 4 time blocks = 1 hour since blocks are 15 min each)
    const hourlyData = [];
    const hours = [];

    // Extract unique hours from actual time blocks (not hardcoded 0-23)
    const uniqueHours = new Set();
    timeBlocks.forEach(tb => {
        // Extract hour from time block (e.g., "17:00:00" -> "17:00")
        const hour = tb.substring(0, 5); // Gets "HH:MM"
        const hourOnly = hour.substring(0, 2); // Gets "HH"
        uniqueHours.add(hourOnly);
    });

    // Create sorted hour labels from unique hours found in data
    const sortedHours = Array.from(uniqueHours).sort();
    sortedHours.forEach(h => {
        hours.push(`${h}:00`);
    });

    // Create a mapping of time blocks to their index in the timeBlocks array
    const timeBlockIndexMap = {};
    timeBlocks.forEach((tb, idx) => {
        timeBlockIndexMap[tb] = idx;
    });

    // Aggregate data by hour for each date
    dates.forEach((date, dateIdx) => {
        const dateRow = matrix[dateIdx];

        hours.forEach(hour => {
            // Find all time blocks that belong to this hour
            const hourPrefix = hour.substring(0, 2); // Get "HH" from "HH:00"
            let hourValues = [];

            timeBlocks.forEach((tb, blockIdx) => {
                if (tb.startsWith(hourPrefix + ':')) {
                    if (blockIdx < dateRow.length && dateRow[blockIdx] !== null) {
                        hourValues.push(dateRow[blockIdx]);
                    }
                }
            });

            if (hourValues.length > 0) {
                const avgValue = hourValues.reduce((a, b) => a + b, 0) / hourValues.length;
                hourlyData.push({
                    x: date,
                    y: hour,
                    v: avgValue
                });
            }
        });
    });

    // Find min/max for color scaling
    const values = hourlyData.map(d => d.v);
    const minVal = Math.min(...values);
    const maxVal = Math.max(...values);

    // Create gradient color function
    function getColor(value) {
        const normalized = (value - minVal) / (maxVal - minVal);

        // Color gradient: blue (low) -> green (mid) -> yellow -> red (high)
        if (normalized < 0.25) {
            // Blue to cyan
            const t = normalized / 0.25;
            return `rgba(${Math.round(0 + t * 100)}, ${Math.round(100 + t * 155)}, ${Math.round(255 - t * 50)}, 0.8)`;
        } else if (normalized < 0.5) {
            // Cyan to green
            const t = (normalized - 0.25) / 0.25;
            return `rgba(${Math.round(100 + t * 55)}, ${Math.round(255 - t * 55)}, ${Math.round(205 - t * 205)}, 0.8)`;
        } else if (normalized < 0.75) {
            // Green to yellow
            const t = (normalized - 0.5) / 0.25;
            return `rgba(${Math.round(155 + t * 100)}, ${Math.round(200 + t * 55)}, 0, 0.8)`;
        } else {
            // Yellow to red
            const t = (normalized - 0.75) / 0.25;
            return `rgba(255, ${Math.round(255 - t * 100)}, 0, 0.8)`;
        }
    }

    // Transform data for Chart.js matrix display (using bubble chart as heat map)
    const chartData = hourlyData.map(point => ({
        x: point.x,
        y: point.y,
        r: 15, // Fixed bubble size
        value: point.v,
        backgroundColor: getColor(point.v)
    }));

    chartInstances[chartId] = new Chart(ctx, {
        type: 'bubble',
        data: {
            datasets: [{
                label: `${metricName} (${unit})`,
                data: chartData,
                backgroundColor: chartData.map(d => d.backgroundColor),
                borderColor: chartData.map(d => d.backgroundColor),
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            layout: {
                padding: {
                    left: 10,
                    right: 20,
                    top: 20,
                    bottom: 10
                }
            },
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    callbacks: {
                        label: function(context) {
                            const point = context.raw;
                            return `${point.y} on ${point.x}: ${point.value.toFixed(2)} ${unit}`;
                        }
                    }
                }
            },
            scales: {
                x: {
                    type: 'category',
                    labels: dates,
                    title: {
                        display: true,
                        text: 'Date',
                        padding: { top: 10, bottom: 0 }
                    },
                    ticks: {
                        maxRotation: 90,
                        minRotation: 45,
                        autoSkip: true,
                        maxTicksLimit: 15,
                        padding: 5
                    },
                    grid: {
                        offset: true,
                        drawOnChartArea: false
                    }
                },
                y: {
                    type: 'category',
                    labels: hours,
                    title: {
                        display: true,
                        text: 'Hour of Day',
                        padding: { left: 0, right: 10 }
                    },
                    ticks: {
                        padding: 5
                    },
                    grid: {
                        offset: true,
                        drawOnChartArea: false
                    }
                }
            }
        }
    });
}

// Send Message
async function sendMessage() {
    const input = document.getElementById('queryInput');
    const query = input.value.trim();

    if (!query) return;

    // Add user message
    addUserMessage(query);

    // Clear input
    input.value = '';
    adjustTextareaHeight();

    // Show typing indicator
    showTypingIndicator();

    // Disable send button
    const sendButton = document.getElementById('sendButton');
    sendButton.disabled = true;

    try {
        // Check if this is a multi-time-slot request (special case)
        const queryLower = query.toLowerCase();
        const isHeatMapOrChartQuery = queryLower.includes('heat map') || queryLower.includes('heatmap') ||
                                       queryLower.includes('heat-map') || queryLower.includes('bar chart') ||
                                       queryLower.includes('line chart') || queryLower.includes('line graph');

        // Check if query has multiple time slots (e.g., "9AM to 5PM, 5PM to 9PM")
        const timeSlotPattern = /(\d{1,2})\s*([ap]m)\s+to\s+(\d{1,2})\s*([ap]m)/gi;
        const timeSlotMatches = query.match(timeSlotPattern);
        const hasMultipleTimeSlots = timeSlotMatches && timeSlotMatches.length > 1;

        // Check if query mentions "all 3 markets" or "all markets"
        const hasMultipleMarkets = queryLower.includes('all 3 markets') || queryLower.includes('all markets');

        // If it's a multi-time-slot query, use the multi-timeslot endpoint
        if (isHeatMapOrChartQuery && (hasMultipleTimeSlots || hasMultipleMarkets)) {
            await handleMultiTimeSlotRequest(query);
            return;
        }

        // If it's a single time slot heat map, use the existing heat map endpoint
        if (queryLower.includes('heat map') || queryLower.includes('heatmap') || queryLower.includes('heat-map')) {
            const queryParams = parseQueryIntent(query);
            await handleHeatMapRequest(query, queryParams);
            return;
        }

        // Send ALL queries to the unified endpoint - it will intelligently route them
        const response = await fetch(`${API_BASE}/api/query`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ question: query })
        });

        const result = await response.json();

        // Hide typing indicator
        hideTypingIndicator();

        // Check if it's an AI response or data response
        if (result.answer) {
            // AI response from NaturalLanguageEngine
            displayAIResponse(result);
        } else {
            // Structured data response from IEXDataService
            await processQueryResult(query, result);
        }

    } catch (error) {
        console.error('Query error:', error);
        hideTypingIndicator();
        addAssistantMessage(`❌ Error: ${error.message}`);
    } finally {
        sendButton.disabled = false;
    }
}

// Display AI-generated intelligent response
function displayAIResponse(result) {
    let content = result.answer;

    // Add key findings if available
    if (result.keyFindings && result.keyFindings.length > 0) {
        content += '\n\n**🔑 Key Findings:**\n';
        result.keyFindings.forEach(finding => {
            content += `- ${finding}\n`;
        });
    }

    // Add recommendations if available
    if (result.recommendations && result.recommendations.length > 0) {
        content += '\n\n**💡 Recommendations:**\n';
        result.recommendations.forEach(rec => {
            content += `- ${rec}\n`;
        });
    }

    // Check if we have charts - if so, use side-by-side layout
    const hasCharts = result.charts && result.charts.length > 0;

    if (hasCharts) {
        // Create side-by-side layout
        addAssistantMessageWithCharts(content, result.charts);
    } else {
        // Regular message without charts
        addAssistantMessage(content);
    }
}

// Intelligent chart type selection based on data characteristics
function selectBestChartType(chartData) {
    const dataPointCount = chartData.labels.length;
    const datasetCount = chartData.datasets.length;

    // If chart type is already specified and reasonable, use it
    if (chartData.chartType && chartData.chartType.toLowerCase() !== 'line') {
        return chartData.chartType.toLowerCase();
    }

    // Time series data (forecasts, trends over time)
    const hasTimeLabels = chartData.labels.some(label =>
        /\d{4}/.test(label) || // Contains year
        /(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)/i.test(label) || // Month names
        /\d{1,2}:\d{2}/.test(label) // Time format
    );

    // Comparison data (markets, categories)
    const isComparison = chartData.labels.every(label =>
        label.length < 10 && // Short labels
        !/\d{4}/.test(label) // No years
    );

    // Multiple datasets usually means we want to compare
    if (datasetCount > 1) {
        if (hasTimeLabels) return 'line'; // Multiple trends over time
        if (isComparison && dataPointCount <= 5) return 'bar'; // Comparing categories
        return 'line'; // Default for multiple series
    }

    // Single dataset
    if (dataPointCount <= 5 && isComparison) {
        return 'bar'; // Few categories to compare
    }

    if (dataPointCount > 20 && hasTimeLabels) {
        return 'line'; // Many time points
    }

    if (dataPointCount >= 6 && dataPointCount <= 20) {
        return hasTimeLabels ? 'line' : 'bar';
    }

    // Default
    return 'line';
}

// Professional color palette matching theme - blue-centric with complementary accents
const chartColors = {
    primaryBlue: 'rgba(15, 76, 129, 0.8)',
    primaryBlueBorder: 'rgba(15, 76, 129, 1)',
    secondaryBlue: 'rgba(26, 127, 184, 0.8)',
    secondaryBlueBorder: 'rgba(26, 127, 184, 1)',
    accentCyan: 'rgba(0, 168, 204, 0.8)',
    accentCyanBorder: 'rgba(0, 168, 204, 1)',
    lightBlue: 'rgba(56, 189, 248, 0.8)',
    lightBlueBorder: 'rgba(56, 189, 248, 1)',
    teal: 'rgba(20, 184, 166, 0.8)',
    tealBorder: 'rgba(20, 184, 166, 1)',
    emerald: 'rgba(16, 185, 129, 0.8)',
    emeraldBorder: 'rgba(16, 185, 129, 1)',
    amber: 'rgba(251, 146, 60, 0.8)',
    amberBorder: 'rgba(251, 146, 60, 1)',
    navy: 'rgba(30, 58, 138, 0.8)',
    navyBorder: 'rgba(30, 58, 138, 1)',
};

const colorPalette = [
    { bg: chartColors.primaryBlue, border: chartColors.primaryBlueBorder },
    { bg: chartColors.accentCyan, border: chartColors.accentCyanBorder },
    { bg: chartColors.teal, border: chartColors.tealBorder },
    { bg: chartColors.secondaryBlue, border: chartColors.secondaryBlueBorder },
    { bg: chartColors.emerald, border: chartColors.emeraldBorder },
    { bg: chartColors.lightBlue, border: chartColors.lightBlueBorder },
    { bg: chartColors.amber, border: chartColors.amberBorder },
    { bg: chartColors.navy, border: chartColors.navyBorder }
];

// Render AI-generated charts with intelligence and interactivity
function renderAIChart(chartData, index) {
    const chartId = `ai-chart-${Date.now()}-${index}`;

    // Add chart container to the last assistant message
    const messagesArea = document.getElementById('messagesArea');
    const lastMessage = messagesArea.querySelector('.message-assistant:last-child .message-text');

    if (lastMessage) {
        const chartContainer = document.createElement('div');
        chartContainer.className = 'chart-container';
        chartContainer.innerHTML = `
            <div class="chart-header">
                <div class="chart-title">${chartData.title}</div>
                <div class="chart-actions">
                    <button class="chart-action-btn" onclick="downloadChart('${chartId}')" title="Download Chart">📥</button>
                    <button class="chart-action-btn" onclick="toggleChartType('${chartId}')" title="Switch Chart Type">📊</button>
                </div>
            </div>
            <canvas id="${chartId}" style="max-height: 450px;"></canvas>
        `;
        lastMessage.appendChild(chartContainer);

        // Render chart
        setTimeout(() => {
            renderChartCanvas(chartId, chartData);
        }, 50);
    }
}

// Render AI-generated charts in a specific column (for side-by-side layout)
function renderAIChartInColumn(chartData, index, columnId) {
    const chartId = `ai-chart-${Date.now()}-${index}`;
    const chartsColumn = document.getElementById(columnId);

    if (chartsColumn) {
        const chartContainer = document.createElement('div');
        chartContainer.className = 'chart-container';
        chartContainer.innerHTML = `
            <div class="chart-header">
                <div class="chart-title">${chartData.title}</div>
                <div class="chart-actions">
                    <button class="chart-action-btn" onclick="downloadChart('${chartId}')" title="Download Chart">📥</button>
                    <button class="chart-action-btn" onclick="toggleChartType('${chartId}')" title="Switch Chart Type">📊</button>
                </div>
            </div>
            <canvas id="${chartId}" style="max-height: 450px;"></canvas>
        `;
        chartsColumn.appendChild(chartContainer);

        // Render chart
        setTimeout(() => {
            renderChartCanvas(chartId, chartData);
        }, 50);
    }
}

// Render chart directly into response container (for new grid layout)
function renderAIChartDirect(chartData, index, responseContainer) {
    const chartId = `ai-chart-${Date.now()}-${index}`;

    const chartContainer = document.createElement('div');
    chartContainer.className = 'chart-container';
    chartContainer.innerHTML = `
        <div class="chart-header">
            <div class="chart-title">${chartData.title}</div>
            <div class="chart-actions">
                <button class="chart-action-btn" onclick="downloadChart('${chartId}')" title="Download Chart">📥</button>
                <button class="chart-action-btn" onclick="toggleChartType('${chartId}')" title="Switch Chart Type">📊</button>
            </div>
        </div>
        <canvas id="${chartId}" style="max-height: 450px;"></canvas>
    `;

    // Insert chart before the analysis column
    const analysisColumn = responseContainer.querySelector('.analysis-column');
    if (analysisColumn) {
        responseContainer.insertBefore(chartContainer, analysisColumn);
    } else {
        responseContainer.appendChild(chartContainer);
    }

    // Render chart
    setTimeout(() => {
        renderChartCanvas(chartId, chartData);
    }, 50);
}

// Common chart rendering logic
function renderChartCanvas(chartId, chartData) {
    const ctx = document.getElementById(chartId);
    if (!ctx) return;

    // Destroy existing chart if any
    if (chartInstances[chartId]) {
        chartInstances[chartId].destroy();
    }

    // Check if this is a combined chart with dual Y-axes (check both camelCase and snake_case)
    const hasDualAxes = chartData.hasDualAxes === true || chartData.has_dual_axes === true || chartData.chartType === 'combined_bar_line';

    // Intelligent chart type selection
    const bestChartType = hasDualAxes ? 'bar' : selectBestChartType(chartData);

    // Prepare datasets with enhanced styling
    const datasets = chartData.datasets.map((dataset, idx) => {
        const colorScheme = colorPalette[idx % colorPalette.length];
        const isArea = bestChartType === 'line' && chartData.datasets.length === 1;

        // For combined charts, preserve backend properties
        if (hasDualAxes) {
            return {
                label: dataset.label,
                data: dataset.data.map(d => parseFloat(d)),
                type: dataset.type || 'bar',
                backgroundColor: dataset.backgroundColor,
                borderColor: dataset.borderColor,
                borderWidth: dataset.borderWidth || 2.5,
                fill: dataset.fill !== undefined ? dataset.fill : false,
                tension: dataset.tension !== undefined && dataset.tension !== null ? parseFloat(dataset.tension) : 0.4,
                pointRadius: dataset.type === 'line' ? 4 : 0,
                pointHoverRadius: dataset.type === 'line' ? 6 : 0,
                pointBackgroundColor: dataset.borderColor,
                pointBorderColor: '#fff',
                pointBorderWidth: 2,
                pointHoverBackgroundColor: '#fff',
                pointHoverBorderColor: dataset.borderColor,
                pointHoverBorderWidth: 2,
                yAxisID: dataset.yAxisID,
                order: dataset.order || 1
            };
        }

        return {
            label: dataset.label,
            data: dataset.data.map(d => parseFloat(d)),
            backgroundColor: isArea ? 'rgba(15, 76, 129, 0.15)' : (dataset.color || colorScheme.bg),
            borderColor: dataset.borderColor || colorScheme.border,
            borderWidth: 2.5,
            fill: dataset.fill !== undefined ? dataset.fill : isArea,
            tension: dataset.tension !== undefined && dataset.tension !== null ? parseFloat(dataset.tension) : 0.4,
            pointRadius: bestChartType === 'line' ? 4 : 0,
            pointHoverRadius: bestChartType === 'line' ? 6 : 0,
            pointBackgroundColor: colorScheme.border,
            pointBorderColor: '#fff',
            pointBorderWidth: 2,
            pointHoverBackgroundColor: '#fff',
            pointHoverBorderColor: colorScheme.border,
            pointHoverBorderWidth: 2
        };
    });

    chartInstances[chartId] = new Chart(ctx, {
        type: bestChartType,
        data: {
            labels: chartData.labels,
            datasets: datasets
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            interaction: {
                mode: 'index',
                intersect: false
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                    labels: {
                        usePointStyle: true,
                        padding: 15,
                        font: {
                            size: 12,
                            weight: '500'
                        }
                    }
                },
                tooltip: {
                    enabled: true,
                    backgroundColor: 'rgba(30, 41, 59, 0.95)',
                    titleColor: '#fff',
                    bodyColor: '#fff',
                    borderColor: 'rgba(15, 76, 129, 1)',
                    borderWidth: 2,
                    padding: 12,
                    displayColors: true,
                    callbacks: {
                        title: function(tooltipItems) {
                            return tooltipItems[0].label;
                        },
                        label: function(context) {
                            let label = context.dataset.label || '';
                            if (label) {
                                label += ': ';
                            }
                            const value = context.parsed.y;

                            // Format based on value magnitude
                            if (value >= 1000) {
                                label += value.toLocaleString(undefined, { maximumFractionDigits: 2 });
                            } else if (value >= 1) {
                                label += value.toFixed(2);
                            } else {
                                label += value.toFixed(4);
                            }

                            // Add unit if detected
                            if (chartData.title.includes('Price') || chartData.title.includes('MCP')) {
                                label += ' ₹/kWh';
                            } else if (chartData.title.includes('Volume') || chartData.title.includes('MCV')) {
                                label += ' GW';
                            }

                            return label;
                        }
                    }
                },
                zoom: {
                    zoom: {
                        wheel: {
                            enabled: true,
                            speed: 0.1
                        },
                        pinch: {
                            enabled: true
                        },
                        mode: 'x'
                    },
                    pan: {
                        enabled: true,
                        mode: 'x'
                    }
                }
            },
            scales: bestChartType !== 'pie' && bestChartType !== 'doughnut' ? (hasDualAxes ? {
                x: {
                    grid: {
                        display: true,
                        color: 'rgba(100, 116, 139, 0.08)',
                        borderColor: 'rgba(100, 116, 139, 0.2)'
                    },
                    ticks: {
                        maxRotation: 45,
                        minRotation: 0,
                        color: '#64748b',
                        font: {
                            size: 11,
                            weight: '500'
                        }
                    }
                },
                'y-mcv': {
                    type: 'linear',
                    position: 'left',
                    beginAtZero: true,
                    grid: {
                        display: true,
                        color: 'rgba(100, 116, 139, 0.08)',
                        borderColor: 'rgba(100, 116, 139, 0.2)'
                    },
                    ticks: {
                        callback: function(value) {
                            if (value >= 1000) {
                                return value.toLocaleString();
                            }
                            return value.toFixed(2);
                        },
                        color: '#00a8cc',
                        font: {
                            size: 11,
                            weight: '500'
                        }
                    },
                    title: {
                        display: true,
                        text: 'Volume (GW)',
                        color: '#00a8cc',
                        font: {
                            size: 12,
                            weight: 'bold'
                        }
                    }
                },
                'y-mcp': {
                    type: 'linear',
                    position: 'right',
                    beginAtZero: true,
                    grid: {
                        display: false
                    },
                    ticks: {
                        callback: function(value) {
                            if (value >= 1000) {
                                return value.toLocaleString();
                            }
                            return value.toFixed(2);
                        },
                        color: '#0f4c81',
                        font: {
                            size: 11,
                            weight: '500'
                        }
                    },
                    title: {
                        display: true,
                        text: 'Price (₹/kWh)',
                        color: '#0f4c81',
                        font: {
                            size: 12,
                            weight: 'bold'
                        }
                    }
                }
            } : {
                x: {
                    grid: {
                        display: true,
                        color: 'rgba(100, 116, 139, 0.08)',
                        borderColor: 'rgba(100, 116, 139, 0.2)'
                    },
                    ticks: {
                        maxRotation: 45,
                        minRotation: 0,
                        color: '#64748b',
                        font: {
                            size: 11,
                            weight: '500'
                        }
                    }
                },
                y: {
                    beginAtZero: true,
                    grid: {
                        display: true,
                        color: 'rgba(100, 116, 139, 0.08)',
                        borderColor: 'rgba(100, 116, 139, 0.2)'
                    },
                    ticks: {
                        callback: function(value) {
                            if (value >= 1000) {
                                return value.toLocaleString();
                            }
                            return value.toFixed(2);
                        },
                        color: '#64748b',
                        font: {
                            size: 11,
                            weight: '500'
                        }
                    },
                    title: {
                        display: true,
                        text: chartData.title.includes('Price') ? 'Price (₹/kWh)' :
                              chartData.title.includes('Volume') ? 'Volume (GW)' : 'Value',
                        font: {
                            size: 12,
                            weight: 'bold'
                        }
                    }
                }
            }) : undefined,
            animation: {
                duration: 750,
                easing: 'easeInOutQuart'
            }
        }
    });
}

// Download chart as image
function downloadChart(chartId) {
    const chart = chartInstances[chartId];
    if (!chart) return;

    const url = chart.toBase64Image();
    const link = document.createElement('a');
    link.download = `chart-${Date.now()}.png`;
    link.href = url;
    link.click();
}

// Toggle between chart types
function toggleChartType(chartId) {
    const chart = chartInstances[chartId];
    if (!chart) return;

    const currentType = chart.config.type;
    const types = ['line', 'bar', 'radar'];
    const currentIndex = types.indexOf(currentType);
    const nextType = types[(currentIndex + 1) % types.length];

    // Get the original data and labels
    const labels = chart.data.labels;
    const datasets = chart.data.datasets.map(ds => ({
        label: ds.label,
        data: ds.data,
    }));

    // Destroy the old chart completely to reset all configurations including axes
    chart.destroy();

    // Get canvas context
    const ctx = document.getElementById(chartId);
    if (!ctx) return;

    // Prepare datasets with new styling
    const styledDatasets = datasets.map((dataset, idx) => {
        const colorScheme = colorPalette[idx % colorPalette.length];
        const isArea = nextType === 'line' && datasets.length === 1;

        return {
            label: dataset.label,
            data: dataset.data,
            backgroundColor: isArea ? 'rgba(15, 76, 129, 0.15)' : colorScheme.bg,
            borderColor: colorScheme.border,
            borderWidth: 2.5,
            fill: isArea,
            tension: nextType === 'line' ? 0.4 : 0,
            pointRadius: nextType === 'line' ? 4 : 0,
            pointHoverRadius: nextType === 'line' ? 6 : 0,
            pointBackgroundColor: colorScheme.border,
            pointBorderColor: '#fff',
            pointBorderWidth: 2,
            pointHoverBackgroundColor: '#fff',
            pointHoverBorderColor: colorScheme.border,
            pointHoverBorderWidth: 2
        };
    });

    // Create new chart with fresh configuration (no dual axes)
    chartInstances[chartId] = new Chart(ctx, {
        type: nextType,
        data: {
            labels: labels,
            datasets: styledDatasets
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            interaction: {
                mode: 'index',
                intersect: false
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                    labels: {
                        usePointStyle: true,
                        padding: 15,
                        font: {
                            size: 12,
                            weight: '500'
                        }
                    }
                },
                tooltip: {
                    enabled: true,
                    backgroundColor: 'rgba(30, 41, 59, 0.95)',
                    titleColor: '#fff',
                    bodyColor: '#fff',
                    borderColor: 'rgba(15, 76, 129, 1)',
                    borderWidth: 2,
                    padding: 12,
                    displayColors: true
                }
            },
            scales: nextType !== 'radar' ? {
                x: {
                    grid: {
                        display: true,
                        color: 'rgba(100, 116, 139, 0.08)',
                        borderColor: 'rgba(100, 116, 139, 0.2)'
                    },
                    ticks: {
                        maxRotation: 45,
                        minRotation: 0,
                        color: '#64748b',
                        font: {
                            size: 11,
                            weight: '500'
                        }
                    }
                },
                y: {
                    beginAtZero: true,
                    grid: {
                        display: true,
                        color: 'rgba(100, 116, 139, 0.08)',
                        borderColor: 'rgba(100, 116, 139, 0.2)'
                    },
                    ticks: {
                        callback: function(value) {
                            if (value >= 1000) {
                                return value.toLocaleString();
                            }
                            return value.toFixed(2);
                        },
                        color: '#64748b',
                        font: {
                            size: 11,
                            weight: '500'
                        }
                    }
                }
            } : {}
        }
    });
}

// Process Query Result with Intelligence
async function processQueryResult(query, result) {
    if (!result.success) {
        addAssistantMessage(`❌ ${result.message}`);
        return;
    }

    const queryLower = query.toLowerCase();

    // Check if result has metadata with chart_data (from grouped aggregations)
    if (result.metadata && result.metadata.chart_data) {
        await showChartFromMetadata(query, result);
        return;
    }

    // Check if result has metadata with groups (aggregation results)
    if (result.metadata && result.metadata.groups) {
        showGroupedAggregationResponse(query, result);
        return;
    }

    // Check if result has simple aggregation metadata
    if (result.metadata && result.metadata.record_count && !result.data?.length) {
        showSimpleAggregationResponse(query, result);
        return;
    }

    // Determine response type based on query intent
    if (queryLower.includes('chart') || queryLower.includes('graph') || queryLower.includes('plot') || queryLower.includes('visualize') || queryLower.includes('compare')) {
        await showChartResponse(query, result);
    } else if (queryLower.includes('statistics') || queryLower.includes('stats') || queryLower.includes('summary') || queryLower.includes('overview')) {
        showStatisticsResponse(query, result);
    } else if (result.data && result.data.length > 0) {
        showDataResponse(query, result);
    } else if (result.aggregations) {
        showAggregationResponse(query, result);
    } else {
        showTextResponse(query, result);
    }
}

// Show Chart from Metadata (from grouped aggregations)
async function showChartFromMetadata(query, result) {
    const metadata = result.metadata;
    const chartData = metadata.chart_data;

    let content = `### 📊 ${generateTitle(query)}\n\n`;
    content += `**${result.message}**\n\n`;

    const chartId = 'chart-' + Date.now();

    content += `<div class="chart-container">
        <div class="chart-header">
            <div class="chart-title">${metadata.aggregation || 'Aggregation'} by ${metadata.group_by || 'Group'}</div>
            <div class="chart-actions">
                <button class="chart-action-btn" onclick="downloadChart('${chartId}')" title="Download Chart">📥</button>
            </div>
        </div>
        <canvas id="${chartId}"></canvas>
    </div>`;

    // Add summary stats
    content += `\n\n**Summary:**\n`;
    content += `- Total Records: ${metadata.total_records.toLocaleString()}\n`;
    content += `- Groups: ${metadata.group_count}\n`;

    addAssistantMessage(content);

    // Render chart after DOM update
    setTimeout(() => {
        renderChartFromData(chartId, chartData, metadata.group_by);
    }, 100);
}

// Show Grouped Aggregation Response
function showGroupedAggregationResponse(query, result) {
    const metadata = result.metadata;
    const groups = metadata.groups;
    const queryLower = query.toLowerCase();

    let content = `### 📊 Analysis Results\n\n`;

    // Generate intelligent narrative based on the query
    const firstGroup = groups[0];
    const columns = Object.keys(firstGroup).filter(k => k !== 'group_key' && k !== 'record_count');

    // Find the highest value for narrative
    let highestGroup = null;
    let highestValue = -Infinity;
    let metricName = '';

    if (columns.length > 0) {
        const metricKey = columns[0]; // Use first metric column
        metricName = formatLabel(metricKey);

        groups.forEach(group => {
            const value = group[metricKey];
            if (typeof value === 'number' && value > highestValue) {
                highestValue = value;
                highestGroup = group;
            }
        });
    }

    // Generate conversational response
    if (queryLower.includes('highest') || queryLower.includes('maximum') || queryLower.includes('peak')) {
        if (highestGroup) {
            content += `Based on the analysis of ${metadata.total_records.toLocaleString()} records, **${highestGroup.group_key}** had the ${queryLower.includes('mcv') ? 'highest MCV (Market Clearing Volume)' : 'highest MCP (Market Clearing Price)'} `;
            content += `with a value of **${formatValue(highestValue)}${queryLower.includes('mcv') ? ' GW' : ' ₹/kWh'}**.\n\n`;
        }
    } else if (queryLower.includes('average') || queryLower.includes('avg')) {
        content += `Here are the average values across different ${metadata.group_by}s based on ${metadata.total_records.toLocaleString()} records:\n\n`;
    } else {
        content += `Analysis complete. Showing ${metadata.aggregation || 'aggregated'} values grouped by ${metadata.group_by}:\n\n`;
    }

    // Create table
    content += '<div class="aggregation-table-container">';
    content += '<table class="data-table"><thead><tr>';
    content += `<th>${formatLabel(metadata.group_by) || 'Group'}</th>`;
    content += '<th>Records</th>';

    // Add column headers with appropriate units
    columns.forEach(col => {
        const colLower = col.toLowerCase();
        let header = formatLabel(col);

        // Add units to column headers
        if (colLower.includes('mcp') || colLower.includes('price')) {
            header += ' (₹/kWh)';
        } else if (colLower.includes('mcv') || colLower.includes('volume')) {
            header += ' (GW)';
        } else if (colLower.includes('demand') || colLower.includes('supply')) {
            header += ' (GW)';
        }

        content += `<th>${header}</th>`;
    });

    content += '</tr></thead><tbody>';

    // Add rows - highlight the highest/max row if relevant
    groups.forEach(group => {
        const isHighest = highestGroup && group.group_key === highestGroup.group_key;
        content += `<tr${isHighest ? ' class="highlight-row"' : ''}>`;
        content += `<td><strong>${group.group_key}</strong></td>`;
        content += `<td>${group.record_count.toLocaleString()}</td>`;

        columns.forEach(col => {
            const value = group[col];
            if (typeof value === 'number') {
                content += `<td><strong>${formatValue(value)}</strong></td>`;
            } else {
                content += `<td>${value}</td>`;
            }
        });

        content += '</tr>';
    });

    content += '</tbody></table></div>';

    // Add insightful summary
    content += `\n\n**Key Insights:**\n`;
    if (highestGroup && (queryLower.includes('highest') || queryLower.includes('which'))) {
        content += `- **${highestGroup.group_key}** stands out with the ${queryLower.includes('mcv') ? 'highest volume' : 'highest value'}\n`;
    }
    content += `- Analyzed ${metadata.total_records.toLocaleString()} records across ${metadata.group_count} ${metadata.group_by}s\n`;
    content += `- Aggregation type: ${metadata.aggregation || 'summary'}\n`;

    addAssistantMessage(content);
}

// Show Simple Aggregation Response
function showSimpleAggregationResponse(query, result) {
    const metadata = result.metadata;

    let content = `### 📊 ${generateTitle(query)}\n\n`;
    content += `**${result.message}**\n\n`;

    content += '<div class="stats-grid">';

    // Create stat cards for all numeric values
    for (const [key, value] of Object.entries(metadata)) {
        if (typeof value === 'number' && key !== 'record_count') {
            const keyLower = key.toLowerCase();
            let unit = '';

            // Determine appropriate unit
            if (keyLower.includes('mcp') || keyLower.includes('price')) {
                unit = ' ₹/kWh';
            } else if (keyLower.includes('mcv') || keyLower.includes('volume')) {
                unit = ' GW';
            } else if (keyLower.includes('demand') || keyLower.includes('supply')) {
                unit = ' GW';
            }

            content += `<div class="stat-card">
                <div class="stat-value">${formatValue(value)}${unit}</div>
                <div class="stat-label">${formatLabel(key)}</div>
            </div>`;
        }
    }

    content += '</div>';

    // Add detailed info
    content += `\n\n**Details:**\n`;
    for (const [key, value] of Object.entries(metadata)) {
        if (typeof value !== 'object') {
            let displayValue = typeof value === 'number' ? formatValue(value) : value;

            // Add units to numeric values in details section
            if (typeof value === 'number') {
                const keyLower = key.toLowerCase();
                if (keyLower.includes('mcp') || keyLower.includes('price')) {
                    displayValue += ' ₹/kWh';
                } else if (keyLower.includes('mcv') || keyLower.includes('volume')) {
                    displayValue += ' GW';
                } else if (keyLower.includes('demand') || keyLower.includes('supply')) {
                    displayValue += ' GW';
                }
            }

            content += `- **${formatLabel(key)}:** ${displayValue}\n`;
        }
    }

    addAssistantMessage(content);
}

// Render Chart from Data
function renderChartFromData(chartId, chartData, groupBy) {
    const ctx = document.getElementById(chartId);
    if (!ctx) return;

    // Destroy existing chart if any
    if (chartInstances[chartId]) {
        chartInstances[chartId].destroy();
    }

    // Determine chart type based on group_by
    let chartType = 'bar';
    if (groupBy === 'month' || groupBy === 'date' || groupBy === 'hour') {
        chartType = 'line';
    }

    chartInstances[chartId] = new Chart(ctx, {
        type: chartType,
        data: chartData,
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: {
                    display: true,
                    position: 'top'
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Value'
                    }
                }
            }
        }
    });
}

// Show Chart Response
async function showChartResponse(query, result) {
    let content = `### 📊 ${generateTitle(query)}\n\n`;

    // Generate chart based on data
    if (result.data && result.data.length > 0) {
        const chartId = 'chart-' + Date.now();

        // Aggregate data for chart
        const aggregated = aggregateDataForChart(result.data, query);

        content += `<div class="chart-container">
            <div class="chart-header">
                <div class="chart-title">${aggregated.title}</div>
                <div class="chart-actions">
                    <button class="chart-action-btn" onclick="downloadChart('${chartId}')" title="Download Chart">📥</button>
                </div>
            </div>
            <canvas id="${chartId}"></canvas>
        </div>`;

        // Add summary
        content += `\n\n**Summary:**\n`;
        content += `- Total Records: ${result.totalRecords.toLocaleString()}\n`;

        if (aggregated.stats) {
            content += `- Average Price: ₹${aggregated.stats.avgPrice.toFixed(4)}/kWh\n`;
            content += `- Peak Price: ₹${aggregated.stats.maxPrice.toFixed(4)}/kWh\n`;
            content += `- Lowest Price: ₹${aggregated.stats.minPrice.toFixed(4)}/kWh\n`;
        }

        addAssistantMessage(content);

        // Render chart after DOM update
        setTimeout(() => {
            renderChart(chartId, aggregated);
        }, 100);
    } else {
        content += 'No data available to create a chart.';
        addAssistantMessage(content);
    }
}

// Aggregate Data for Chart
function aggregateDataForChart(data, query) {
    const queryLower = query.toLowerCase();

    // Check if comparing market types
    if (queryLower.includes('compare') && queryLower.includes('market')) {
        return aggregateByMarketType(data);
    }

    // Check if looking at time series
    if (queryLower.includes('trend') || queryLower.includes('over time') || queryLower.includes('monthly')) {
        return aggregateByTime(data);
    }

    // Default: aggregate by market type
    return aggregateByMarketType(data);
}

// Aggregate by Market Type
function aggregateByMarketType(data) {
    const byType = {};

    data.forEach(row => {
        if (!byType[row.type]) {
            byType[row.type] = {
                count: 0,
                totalPrice: 0,
                totalVolume: 0,
                maxPrice: 0,
                minPrice: Infinity
            };
        }

        byType[row.type].count++;
        byType[row.type].totalPrice += row.mcp;
        byType[row.type].totalVolume += row.mcv;
        byType[row.type].maxPrice = Math.max(byType[row.type].maxPrice, row.mcp);
        byType[row.type].minPrice = Math.min(byType[row.type].minPrice, row.mcp);
    });

    const labels = Object.keys(byType);
    const avgPrices = labels.map(type => (byType[type].totalPrice / byType[type].count).toFixed(4));
    const avgVolumes = labels.map(type => (byType[type].totalVolume / byType[type].count).toFixed(4));

    return {
        title: 'Average Prices by Market Type',
        type: 'bar',
        labels: labels,
        datasets: [
            {
                label: 'Average Price (₹/kWh)',
                data: avgPrices,
                backgroundColor: ['#667eea', '#48bb78', '#ed8936'],
                borderWidth: 0
            }
        ],
        stats: {
            avgPrice: avgPrices.reduce((a, b) => parseFloat(a) + parseFloat(b), 0) / avgPrices.length,
            maxPrice: Math.max(...Object.values(byType).map(v => v.maxPrice)),
            minPrice: Math.min(...Object.values(byType).map(v => v.minPrice))
        }
    };
}

// Aggregate by Time
function aggregateByTime(data) {
    const byMonth = {};

    data.forEach(row => {
        const date = new Date(row.date);
        const monthKey = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;

        if (!byMonth[monthKey]) {
            byMonth[monthKey] = {
                count: 0,
                totalPrice: 0
            };
        }

        byMonth[monthKey].count++;
        byMonth[monthKey].totalPrice += row.mcp;
    });

    const sorted = Object.keys(byMonth).sort();
    const labels = sorted.map(key => key);
    const avgPrices = sorted.map(key => (byMonth[key].totalPrice / byMonth[key].count).toFixed(4));

    return {
        title: 'Price Trend Over Time',
        type: 'line',
        labels: labels,
        datasets: [
            {
                label: 'Average Price (₹/kWh)',
                data: avgPrices,
                borderColor: '#667eea',
                backgroundColor: 'rgba(102, 126, 234, 0.1)',
                tension: 0.4,
                fill: true
            }
        ],
        stats: {
            avgPrice: avgPrices.reduce((a, b) => parseFloat(a) + parseFloat(b), 0) / avgPrices.length,
            maxPrice: Math.max(...avgPrices.map(parseFloat)),
            minPrice: Math.min(...avgPrices.map(parseFloat))
        }
    };
}

// Render Chart
function renderChart(chartId, config) {
    const ctx = document.getElementById(chartId);
    if (!ctx) return;

    // Destroy existing chart if any
    if (chartInstances[chartId]) {
        chartInstances[chartId].destroy();
    }

    chartInstances[chartId] = new Chart(ctx, {
        type: config.type,
        data: {
            labels: config.labels,
            datasets: config.datasets
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: {
                    display: true,
                    position: 'top'
                }
            },
            scales: config.type === 'bar' || config.type === 'line' ? {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Price (₹/kWh)'
                    }
                }
            } : undefined
        }
    });
}

// Show Statistics Response
function showStatisticsResponse(query, result) {
    let content = `### 📈 ${generateTitle(query)}\n\n`;

    if (result.aggregations) {
        const stats = result.aggregations;

        // Create stats cards HTML
        content += '<div class="stats-grid">';

        if (stats.TotalRecords) {
            content += `<div class="stat-card">
                <div class="stat-value">${stats.TotalRecords.toLocaleString()}</div>
                <div class="stat-label">Total Records</div>
            </div>`;
        }

        if (stats.AvgMCP) {
            content += `<div class="stat-card">
                <div class="stat-value">₹${stats.AvgMCP.toFixed(2)}</div>
                <div class="stat-label">Avg MCP (₹/kWh)</div>
            </div>`;
        }

        if (stats.MaxMCP) {
            content += `<div class="stat-card">
                <div class="stat-value">₹${stats.MaxMCP.toFixed(2)}</div>
                <div class="stat-label">Peak Price</div>
            </div>`;
        }

        if (stats.MinMCP) {
            content += `<div class="stat-card">
                <div class="stat-value">₹${stats.MinMCP.toFixed(4)}</div>
                <div class="stat-label">Lowest Price</div>
            </div>`;
        }

        if (stats.AvgMCV) {
            content += `<div class="stat-card">
                <div class="stat-value">${stats.AvgMCV.toFixed(2)} GW</div>
                <div class="stat-label">Avg Volume</div>
            </div>`;
        }

        if (stats.MaxMCV) {
            content += `<div class="stat-card">
                <div class="stat-value">${stats.MaxMCV.toFixed(2)} GW</div>
                <div class="stat-label">Peak Volume</div>
            </div>`;
        }

        content += '</div>';

        // Add market types
        if (stats.MarketTypes) {
            content += `\n\n**Market Types:** ${stats.MarketTypes.join(', ')}\n\n`;
        }

        // Add date range
        if (stats.DateRange) {
            const start = new Date(stats.DateRange.Start).toLocaleDateString();
            const end = new Date(stats.DateRange.End).toLocaleDateString();
            content += `**Date Range:** ${start} to ${end}\n`;
        }
    }

    addAssistantMessage(content);
}

// Show Data Response
function showDataResponse(query, result) {
    let content = `### 📋 ${generateTitle(query)}\n\n`;
    content += `Found **${result.totalRecords.toLocaleString()}** records.\n\n`;

    if (result.data.length > 10) {
        content += `Showing first 10 results:\n\n`;
        content += formatDataTable(result.data.slice(0, 10));
        content += `\n\n*...and ${result.data.length - 10} more records*`;
    } else {
        content += formatDataTable(result.data);
    }

    addAssistantMessage(content);
}

// Show Aggregation Response
function showAggregationResponse(query, result) {
    let content = `### 📊 ${generateTitle(query)}\n\n`;

    const agg = result.aggregations;

    content += '**Results:**\n\n';

    for (const [key, value] of Object.entries(agg)) {
        if (typeof value === 'number') {
            content += `- **${formatLabel(key)}:** ${formatValue(value)}\n`;
        } else if (typeof value === 'string') {
            content += `- **${formatLabel(key)}:** ${value}\n`;
        } else if (Array.isArray(value)) {
            content += `- **${formatLabel(key)}:** ${value.join(', ')}\n`;
        }
    }

    addAssistantMessage(content);
}

// Show Text Response
function showTextResponse(query, result) {
    let content = `### ℹ️ ${generateTitle(query)}\n\n`;
    content += result.message || 'Query completed successfully.';

    if (result.totalRecords !== undefined) {
        content += `\n\n**Records found:** ${result.totalRecords.toLocaleString()}`;
    }

    addAssistantMessage(content);
}

// Format Data Table
function formatDataTable(data) {
    if (!data || data.length === 0) return 'No data available.';

    let html = '<table class="data-table"><thead><tr>';
    html += '<th>Type</th><th>Date</th><th>Time</th><th>MCP (₹/kWh)</th><th>MCV (GW)</th>';
    html += '</tr></thead><tbody>';

    data.forEach(row => {
        const date = new Date(row.date).toLocaleDateString();
        html += `<tr>
            <td>${row.type}</td>
            <td>${date}</td>
            <td>${row.timeBlock}</td>
            <td>₹${row.mcp.toFixed(4)}</td>
            <td>${row.mcv.toFixed(2)}</td>
        </tr>`;
    });

    html += '</tbody></table>';
    return html;
}

// Generate Title
function generateTitle(query) {
    // Capitalize first letter and clean up
    return query.charAt(0).toUpperCase() + query.slice(1);
}

// Format Label
function formatLabel(key) {
    return key.replace(/([A-Z])/g, ' $1').replace(/^./, str => str.toUpperCase()).trim();
}

// Format Value
function formatValue(value) {
    if (typeof value === 'number') {
        if (Number.isInteger(value)) {
            return value.toLocaleString();
        } else {
            return value.toFixed(4);
        }
    }
    return value;
}

// Add User Message
function addUserMessage(text) {
    const messagesArea = document.getElementById('messagesArea');

    const messageDiv = document.createElement('div');
    messageDiv.className = 'message message-user';

    messageDiv.innerHTML = `
        <div class="message-content">
            <div class="message-header">
                <div class="message-avatar">👤</div>
                <span>You</span>
            </div>
            <div class="message-text">${escapeHtml(text)}</div>
        </div>
    `;

    messagesArea.appendChild(messageDiv);
    scrollToBottom();
}

// Add Assistant Message
function addAssistantMessage(content) {
    const messagesArea = document.getElementById('messagesArea');

    const messageDiv = document.createElement('div');
    messageDiv.className = 'message message-assistant';

    // Convert markdown to HTML using marked library
    const htmlContent = marked.parse(content);

    messageDiv.innerHTML = `
        <div class="message-content">
            <div class="message-header">
                <div class="message-avatar">🤖</div>
                <span>IEX Assistant</span>
            </div>
            <div class="message-text">${htmlContent}</div>
        </div>
    `;

    messagesArea.appendChild(messageDiv);
    scrollToBottom();
}

// Add Assistant Message with Side-by-Side Charts Layout
function addAssistantMessageWithCharts(content, charts) {
    const messagesArea = document.getElementById('messagesArea');

    const messageDiv = document.createElement('div');
    messageDiv.className = 'message message-assistant';

    // Convert markdown to HTML using marked library
    const htmlContent = marked.parse(content);

    const responseId = `response-${Date.now()}`;

    messageDiv.innerHTML = `
        <div class="message-content with-charts">
            <div class="message-header">
                <div class="message-avatar">🤖</div>
                <span>IEX Assistant</span>
            </div>
            <div class="response-with-charts" id="${responseId}">
                <div class="analysis-column">
                    ${htmlContent}
                </div>
            </div>
        </div>
    `;

    messagesArea.appendChild(messageDiv);
    scrollToBottom();

    // Render charts directly into the response container
    const responseContainer = messageDiv.querySelector(`#${responseId}`);
    setTimeout(() => {
        charts.forEach((chartData, index) => {
            renderAIChartDirect(chartData, index, responseContainer);
        });
    }, 100);
}

// Show/Hide Typing Indicator
function showTypingIndicator() {
    document.getElementById('typingIndicator').classList.add('active');
    scrollToBottom();
}

function hideTypingIndicator() {
    document.getElementById('typingIndicator').classList.remove('active');
}

// Scroll to Bottom
function scrollToBottom() {
    const messagesArea = document.getElementById('messagesArea');
    messagesArea.scrollTop = messagesArea.scrollHeight;
}

// Escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
