"""Summarize raw socket benchmark runs; Python standard library only."""
import csv
import hashlib
import json
import math
import statistics
import sys
from pathlib import Path


def percentile(values, fraction):
    values = sorted(values)
    if not values:
        return None
    index = (len(values) - 1) * fraction
    low = math.floor(index)
    high = math.ceil(index)
    return values[low] + (values[high] - values[low]) * (index - low)


def summarize(directory):
    trial = json.loads((directory / "trial.json").read_text())
    if not trial["success"]:
        raise ValueError(f"Incomplete trial: {directory.name}")
    with (directory / "server.csv").open(newline="") as stream:
        rows = [{key: float(value) for key, value in row.items() if key != "mode"}
                for row in csv.DictReader(stream)]
    warm = trial["warm_seconds"] * 30
    end = trial["seconds"] * 30
    # Exclude final tick: END can race client closure/member removal in Metrics.row.
    boundary = next(row for row in rows if row["tick"] == warm)
    measured = [row for row in rows if warm < row["tick"] < end]
    last = measured[-1]
    if len(measured) != end - warm - 1:
        raise ValueError(f"Missing metrics samples: {directory.name}")
    if last["metrics_drop"] != 0:
        raise ValueError(f"Metrics queue dropped samples: {directory.name}")
    wall = last["elapsed_s"] - boundary["elapsed_s"]
    difference = lambda key: last[key] - boundary[key]
    ontime, missing = difference("input_ontime"), difference("input_missing")
    if ontime + missing != trial["players"] * (last["tick"] - warm):
        raise ValueError(f"Input denominator mismatch: {directory.name}")
    clients = json.loads((directory / "clients.json").read_text())
    if len(clients) != trial["players"]:
        raise ValueError(f"Client count mismatch: {directory.name}")
    for client in clients:
        if (not client["ended"] or client["reason"] != 0 or client["invalid"] != 0
                or client["disconnected"] != 0):
            raise ValueError(f"Client validation failed: {directory.name}")
    ages = [sample["age_us"] / 1000 for client in clients
            for sample in client["snapshots"] if sample["tick"] < end]
    rtts = [sample["rtt_us"] / 1000 for client in clients for sample in client["pings"]]
    full, delta = difference("full"), difference("delta")
    return {
        "run_id": trial["id"], "mode": trial["mode"], "profile": trial["profile"]["name"],
        "repeat": trial["repeat"], "seed": trial["seed"], "players": trial["players"],
        "measured_wall_s": wall, "measured_ticks": len(measured),
        "server_offered_udp_Bps": difference("tx_bytes") / wall,
        "server_accepted_udp_Bps": difference("rx_bytes") / wall,
        "server_offered_udp_pps": difference("tx_packets") / wall,
        "server_accepted_udp_pps": difference("rx_packets") / wall,
        "input_ontime": ontime, "input_missing": missing,
        "input_missing_pct": 100 * missing / (ontime + missing),
        "input_late": difference("input_late"), "input_duplicate": difference("input_duplicate"),
        "input_conflict": difference("input_conflict"), "input_drop": difference("input_drop"),
        "tick_p50_us": percentile([row["tick_us"] for row in measured], .5),
        "tick_p95_us": percentile([row["tick_us"] for row in measured], .95),
        "tick_p99_us": percentile([row["tick_us"] for row in measured], .99),
        "encode_p95_us": percentile([row["encode_us"] for row in measured], .95),
        "schedule_lag_p95_us": percentile([row["schedule_lag_us"] for row in measured], .95),
        "overrun_pct": 100 * statistics.mean(row["overrun"] for row in measured),
        "delta_pct": 100 * delta / (full + delta), "full": full, "delta": delta,
        "resync": difference("resync"), "udp_impairment_drop": difference("udp_drop"),
        "invalid_server": difference("invalid"), "invalid_clients": sum(c["invalid"] for c in clients),
        "missing_base_clients": sum(c["missing_base"] for c in clients),
        "rtt_p50_ms": percentile(rtts, .5), "rtt_p95_ms": percentile(rtts, .95),
        "snapshot_age_p95_ms": percentile(ages, .95), "rtt_samples": len(rtts),
        "snapshot_samples": len(ages),
        "heap_peak_MiB": max(row["heap_bytes"] for row in measured) / (1024 ** 2),
        "server_cpu_one_core_pct": difference("process_cpu_ns") / (wall * 1e9) * 100,
        "gc_count": difference("gc_count"), "gc_ms": difference("gc_ms"),
        "command_depth_max": max(row["command_depth"] for row in measured),
    }


def main():
    root = Path(sys.argv[1])
    trials = sorted(path.parent for path in root.glob("*/trial.json"))
    if not trials:
        raise ValueError("No completed trials")
    runs = [summarize(path) for path in trials]
    with (root / "runs.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(runs[0]))
        writer.writeheader()
        writer.writerows(runs)
    keys = [key for key, value in runs[0].items() if isinstance(value, (int, float))
            and key not in {"repeat", "seed", "players"}]
    aggregates = []
    for profile, mode in sorted({(run["profile"], run["mode"]) for run in runs}):
        group = [run for run in runs if run["profile"] == profile and run["mode"] == mode]
        aggregate = {"profile": profile, "mode": mode, "repeats": len(group)}
        for key in keys:
            values = [run[key] for run in group if run[key] is not None]
            aggregate[key + "_mean"] = statistics.mean(values) if values else None
            aggregate[key + "_sd"] = statistics.stdev(values) if len(values) > 1 else 0
        aggregates.append(aggregate)
    (root / "summary.json").write_text(json.dumps(aggregates, indent=2), encoding="utf-8")
    with (root / "summary.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(aggregates[0]))
        writer.writeheader()
        writer.writerows(aggregates)
    modes = ["BASELINE", "INPUT_REDUNDANCY", "DELTA", "COMBINED"]
    profiles = ["clean", "loss5", "delay50_jitter20_loss5"]
    group_lookup = {(item["profile"], item["mode"]): item for item in aggregates}
    def cell(item, key, scale=1, digits=2):
        mean = item.get(key + "_mean")
        if mean is None:
            return "Không có mẫu"
        sd = item[key + "_sd"]
        return f"{mean / scale:.{digits}f} ± {sd / scale:.{digits}f}"
    lines = ["# Kết quả benchmark socket Tank Battle", "",
             f"Đã xác minh **{len(runs)} lượt hợp lệ**, {len(aggregates)} nhóm cấu hình. "
             "Các giá trị bảng là mean ± sample SD giữa các lượt; p95 được tính riêng từng lượt trước khi tổng hợp.", "",
             "## Lưu lượng, input thiếu và thời gian xử lý", "",
             "| Profile | Mode | n | Server TX ứng dụng (KiB/s) | Input thiếu (%) | Tick p95 (µs) | RTT p95 (ms) |",
             "|---|---|---:|---:|---:|---:|---:|"]
    for profile in profiles:
        for mode in modes:
            item = group_lookup.get((profile, mode))
            if item is not None:
                lines.append(f'| {profile} | {mode} | {item["repeats"]} | '
                             f'{cell(item, "server_offered_udp_Bps", 1024)} | '
                             f'{cell(item, "input_missing_pct", digits=3)} | '
                             f'{cell(item, "tick_p95_us", digits=1)} | {cell(item, "rtt_p95_ms")} |')
    lines += ["", "## Chi phí server và đồng bộ", "",
              "| Profile | Mode | Delta (%) | Encode p95 (µs) | CPU server (% một lõi) | Snapshot age p95 (ms) |",
              "|---|---|---:|---:|---:|---:|"]
    for profile in profiles:
        for mode in modes:
            item = group_lookup.get((profile, mode))
            if item is not None:
                lines.append(f'| {profile} | {mode} | {cell(item, "delta_pct")} | '
                             f'{cell(item, "encode_p95_us", digits=1)} | '
                             f'{cell(item, "server_cpu_one_core_pct")} | '
                             f'{cell(item, "snapshot_age_p95_ms")} |')
    lines += ["", "## Đối chiếu với BASELINE", ""]
    if all(item["mode"] == "BASELINE" for item in aggregates):
        lines += ["Bộ dữ liệu này chỉ chạy BASELINE. Không có phép đối chiếu cải tiến trong cùng lượt; "
                  "không tự gộp với bộ benchmark bốn mode trước đó.", ""]
    for profile in profiles:
        base = group_lookup.get((profile, "BASELINE"))
        combined = group_lookup.get((profile, "COMBINED"))
        if base and combined:
            tx_reduction = (1 - combined["server_offered_udp_Bps_mean"] / base["server_offered_udp_Bps_mean"]) * 100
            lines.append(f'- `{profile}`: COMBINED giảm {tx_reduction:.2f}% byte ứng dụng server đưa vào transport; '
                         f'input thiếu {base["input_missing_pct_mean"]:.3f}% → {combined["input_missing_pct_mean"]:.3f}%.')
    lines += ["", "## Phạm vi và giới hạn", "",
              "- 4 client Java headless, một phòng, IPv4 loopback; server ở JVM riêng. Không chạy rendering/prediction Unity.",
              "- Profile clean tắt impairment; loss5 chèn loss 5%; profile còn lại chèn loss 5%, delay 50 ms và jitter ±20 ms mỗi chiều tại server.",
              "- Trận chính thức 30 giây, warm-up 5 giây, 3 seed; pilot 6 giây ở thư mục khác không gộp vào kết quả chính thức.",
              "- Rate server lấy tick 150 làm mốc và đo tick 151–899 (749 tick/lượt). Dùng chênh lệch counter và thời gian wall-clock.",
              "- TX là frame ứng dụng trước impairment, không gồm header mạng và không phải số byte thực tới client. RX chấp nhận tại server có trong runs.csv.",
              "- RTT chỉ lấy ping/pong nhận được; snapshot age phụ thuộc offset ước lượng và không bằng độ trễ một chiều chính xác.",
              "- Cùng seed không đồng nghĩa từng gói gặp cùng sự kiện loss giữa các mode, vì số lượng/thời điểm gói có thể khác.",
              "- Các thế giới có thể diễn tiến khác vì input mất; đây là tác động thực của cơ chế mạng, không phải mọi snapshot có nội dung byte giống hệt.",
              "- Warm-up ngắn, n=3 và máy không được cô lập khỏi tác vụ nền; chưa tuyên bố ý nghĩa thống kê, tải tối đa hoặc kết quả LAN/Internet.",
              "- Nếu không có RESYNC thì không suy ra thời gian phục hồi từ giá trị 0; chưa có benchmark burst loss riêng.", "",
              "## Dữ liệu tái lập", "",
              "[Kết quả từng lượt](runs.csv), [bảng tổng hợp](summary.csv), [JSON tổng hợp](summary.json), "
              "[hash tệp](hashes.json), [môi trường](environment.json), "
              "[phương pháp và mã công cụ](../../../scripts/benchmark/README.md).", "",
              "Mỗi thư mục con giữ trial.json, server.properties, server.csv, clients.json và log. "
              "Analyzer dừng nếu lượt không thành công, thiếu mẫu hoặc mẫu số input không khớp.", ""]
    (root / "results.md").write_text("\n".join(lines), encoding="utf-8")
    manifest = {}
    for path in sorted(root.rglob("*")):
        if path.is_file() and path.name not in {"hashes.json"}:
            manifest[path.relative_to(root).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    (root / "hashes.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Validated and summarized {len(runs)} runs in {len(aggregates)} groups")
    for item in aggregates:
        print(f'{item["profile"]:25} {item["mode"]:17} n={item["repeats"]} '
              f'TX={item["server_offered_udp_Bps_mean"]:.1f} B/s '
              f'missing={item["input_missing_pct_mean"]:.3f}% '
              f'tick_p95={item["tick_p95_us_mean"]:.1f} us '
              f'RTT_p95={item["rtt_p95_ms_mean"]:.2f} ms')


if __name__ == "__main__":
    main()
